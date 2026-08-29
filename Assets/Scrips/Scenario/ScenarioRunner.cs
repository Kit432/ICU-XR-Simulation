using System;
using System.Collections.Generic;
using System.Linq;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioRunner
    {
        private readonly Dictionary<string, ScenarioNode> nodesById;
        private readonly HashSet<string> activeGlobalRuleIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly RuleEvaluator ruleEvaluator;
        private readonly EffectExecutor effectExecutor;
        private bool timeoutActive;
        private float timeoutRemaining;

        public event Action<ScenarioRunner> ScenarioStarted;
        public event Action<ScenarioNode> NodeEntered;
        public event Action<ScenarioOptionSelectedEvent> OptionSelected;
        public event Action<ScenarioState> StateChanged;
        public event Action<ScenarioVitalsChangedEvent> VitalsChanged;
        public event Action<ScenarioScoreChangedEvent> ScoreChanged;
        public event Action<ScenarioFlagChangedEvent> FlagChanged;
        public event Action<ScenarioTimeoutEvent> TimeoutTriggered;
        public event Action<ScenarioGlobalRuleEvent> GlobalRuleActivated;
        public event Action<ScenarioGlobalRuleEvent> GlobalRuleDeactivated;
        public event Action<ScenarioGateEvent> GateBlocked;
        public event Action<ScenarioGateEvent> GatePassed;
        public event Action<ScenarioState> ScenarioCompleted;
        public event Action<ScenarioFeedbackRequest> FeedbackRequested;
        public event Action<ScenarioUiEffectRequest> UiEffectRequested;
        public event Action<string> WarningRaised;

        public ScenarioDefinition Definition { get; }
        public ScenarioState State { get; }
        public ScenarioNode CurrentNode { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsTimeoutActive => timeoutActive;
        public float TimeoutRemaining => timeoutActive ? Math.Max(0f, timeoutRemaining) : 0f;
        public bool IsWaitingAtGate => IsRunning && IsNodeType(CurrentNode, "gate");
        public IReadOnlyList<ScenarioOption> CurrentOptions
        {
            get
            {
                if (IsNodeType(CurrentNode, "decision") && CurrentNode.Options != null)
                {
                    return CurrentNode.Options;
                }

                return Array.Empty<ScenarioOption>();
            }
        }

        public Func<string, string, bool> GateRequirementResolver { get; set; }

        public ScenarioRunner(
            ScenarioDefinition definition,
            ScenarioState state,
            RuleEvaluator ruleEvaluator = null,
            EffectExecutor effectExecutor = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = state ?? throw new ArgumentNullException(nameof(state));

            ScenarioValidationResult validation = ScenarioValidator.Validate(definition);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.FormatErrors(), nameof(definition));
            }

            nodesById = definition.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            this.ruleEvaluator = ruleEvaluator ?? new RuleEvaluator();
            this.effectExecutor = effectExecutor ?? new EffectExecutor();
            SubscribeToServices();
        }

        public bool Start()
        {
            if (Definition.Nodes == null || Definition.Nodes.Count == 0)
            {
                WarningRaised?.Invoke("The loaded scenario has no start node.");
                return false;
            }

            Stop();
            activeGlobalRuleIds.Clear();
            IsRunning = true;
            IsCompleted = false;
            ScenarioStarted?.Invoke(this);
            EnterNode(Definition.Nodes[0]);
            EvaluateGlobalRules();
            return true;
        }

        public void Stop()
        {
            CancelTimeout();
            IsRunning = false;
            IsCompleted = false;
            CurrentNode = null;
            activeGlobalRuleIds.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f)
            {
                return;
            }

            State.ElapsedTime += deltaTime;
            EvaluateGlobalRules();

            if (!timeoutActive)
            {
                return;
            }

            timeoutRemaining -= deltaTime;
            if (timeoutRemaining > 0f)
            {
                return;
            }

            TriggerTimeout();
        }

        public bool ContinueMessage()
        {
            if (!IsRunning || !IsNodeType(CurrentNode, "message"))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentNode.NextNodeId))
            {
                WarningRaised?.Invoke($"Message node '{CurrentNode.Id}' has no next_node_id.");
                return false;
            }

            return TransitionTo(CurrentNode.NextNodeId);
        }

        public bool TryHandleHotspotInteraction(string hotspotId)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(hotspotId) || CurrentNode == null)
            {
                return false;
            }

            if (IsNodeType(CurrentNode, "gate"))
            {
                if (string.Equals(
                        CurrentNode.GateRequirements?.TargetHotspot,
                        hotspotId,
                        StringComparison.Ordinal))
                {
                    EvaluateCurrentGate();
                }

                return false;
            }

            if (!IsNodeType(CurrentNode, "decision") || CurrentNode.Options == null)
            {
                return false;
            }

            ScenarioOption matchingOption = CurrentNode.Options.FirstOrDefault(option =>
                option != null &&
                string.Equals(option.TargetHotspot, hotspotId, StringComparison.Ordinal));

            if (matchingOption == null)
            {
                effectExecutor.RequestFeedback(
                    "This hotspot is not an available action for the current decision.",
                    ScenarioFeedbackStyle.Info,
                    CurrentNode.Id);
                return false;
            }

            ScenarioNode resolvedNode = CurrentNode;
            CancelTimeout();
            OptionSelected?.Invoke(new ScenarioOptionSelectedEvent(resolvedNode, matchingOption, hotspotId));
            effectExecutor.ApplyBundle(matchingOption.Effects, State, matchingOption.Id);
            EvaluateGlobalRules();
            return TransitionTo(matchingOption.NextNodeId);
        }

        public GateEvaluationResult EvaluateCurrentGate()
        {
            if (!IsRunning || !IsNodeType(CurrentNode, "gate"))
            {
                return new GateEvaluationResult(false, new[] { "No active gate" });
            }

            GateEvaluationResult evaluation = ruleEvaluator.EvaluateGateRequirements(
                CurrentNode.GateRequirements,
                GateRequirementResolver);

            if (evaluation.IsSatisfied)
            {
                PassCurrentGate(evaluation, false);
            }
            else
            {
                GateBlocked?.Invoke(new ScenarioGateEvent(CurrentNode, evaluation, false));
                effectExecutor.RequestFeedback(
                    CurrentNode.FeedbackBlocked,
                    ScenarioFeedbackStyle.Warning,
                    CurrentNode.Id);
            }

            return evaluation;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugBypassCurrentGate()
        {
            if (!IsRunning || !IsNodeType(CurrentNode, "gate"))
            {
                return false;
            }

            PassCurrentGate(new GateEvaluationResult(true, Array.Empty<string>()), true);
            return true;
        }
#endif

        public void EvaluateGlobalRules()
        {
            IReadOnlyList<GlobalRuleDefinition> rules = Definition.Rules?.GlobalRules;
            if (rules == null)
            {
                return;
            }

            foreach (GlobalRuleDefinition rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id))
                {
                    WarningRaised?.Invoke("A global rule has no id and was skipped.");
                    continue;
                }

                bool conditionIsTrue = ruleEvaluator.EvaluateGlobalRule(rule, State);
                if (!conditionIsTrue)
                {
                    if (activeGlobalRuleIds.Remove(rule.Id))
                    {
                        GlobalRuleDeactivated?.Invoke(new ScenarioGlobalRuleEvent(rule));
                        effectExecutor.ApplyEffects(rule.Effects, State, rule.Id, false);
                    }

                    continue;
                }

                if (!activeGlobalRuleIds.Add(rule.Id))
                {
                    continue;
                }

                GlobalRuleActivated?.Invoke(new ScenarioGlobalRuleEvent(rule));
                effectExecutor.ApplyEffects(rule.Effects, State, rule.Id);
            }
        }

        private void EnterNode(ScenarioNode node)
        {
            CancelTimeout();
            CurrentNode = node;
            State.CurrentNodeId = node.Id;
            StateChanged?.Invoke(State);
            NodeEntered?.Invoke(node);

            if (IsNodeType(node, "end"))
            {
                IsCompleted = true;
                IsRunning = false;
                ScenarioCompleted?.Invoke(State);
                return;
            }

            if (IsNodeType(node, "gate"))
            {
                EvaluateCurrentGate();
                return;
            }

            if (node.Timeout != null && node.Timeout.Seconds > 0f)
            {
                timeoutActive = true;
                timeoutRemaining = node.Timeout.Seconds;
            }
        }

        private bool TransitionTo(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                WarningRaised?.Invoke($"Node '{CurrentNode?.Id}' cannot transition because next_node_id is empty.");
                return false;
            }

            if (!nodesById.TryGetValue(nodeId, out ScenarioNode nextNode))
            {
                WarningRaised?.Invoke($"Cannot transition to unknown node '{nodeId}'.");
                return false;
            }

            EnterNode(nextNode);
            return true;
        }

        private void TriggerTimeout()
        {
            ScenarioNode timedOutNode = CurrentNode;
            TimeoutDefinition timeout = timedOutNode?.Timeout;
            CancelTimeout();

            if (timeout == null)
            {
                return;
            }

            TimeoutTriggered?.Invoke(new ScenarioTimeoutEvent(timedOutNode, timeout.Seconds));
            effectExecutor.ApplyBundle(
                timeout.OnTimeoutEffects,
                State,
                timedOutNode.Id,
                ScenarioFeedbackStyle.Warning);
            EvaluateGlobalRules();
            TransitionTo(timeout.NextNodeId);
        }

        private void PassCurrentGate(GateEvaluationResult evaluation, bool debugBypassed)
        {
            ScenarioNode gateNode = CurrentNode;
            effectExecutor.ApplyBundle(
                gateNode.EffectsOnPass,
                State,
                gateNode.Id,
                ScenarioFeedbackStyle.Success);

            if (!string.IsNullOrWhiteSpace(gateNode.FeedbackSuccess))
            {
                effectExecutor.RequestFeedback(
                    gateNode.FeedbackSuccess,
                    ScenarioFeedbackStyle.Success,
                    gateNode.Id);
            }

            GatePassed?.Invoke(new ScenarioGateEvent(gateNode, evaluation, debugBypassed));
            EvaluateGlobalRules();
            TransitionTo(gateNode.NextNodeId);
        }

        private void CancelTimeout()
        {
            timeoutActive = false;
            timeoutRemaining = 0f;
        }

        private void SubscribeToServices()
        {
            effectExecutor.StateChanged += state => StateChanged?.Invoke(state);
            effectExecutor.ScoreChanged += change => ScoreChanged?.Invoke(change);
            effectExecutor.VitalsChanged += change => VitalsChanged?.Invoke(change);
            effectExecutor.FlagChanged += change => FlagChanged?.Invoke(change);
            effectExecutor.FeedbackRequested += request => FeedbackRequested?.Invoke(request);
            effectExecutor.UiEffectRequested += request => UiEffectRequested?.Invoke(request);
            effectExecutor.WarningRaised += warning => WarningRaised?.Invoke(warning);
            ruleEvaluator.WarningRaised += warning => WarningRaised?.Invoke(warning);
        }

        private static bool IsNodeType(ScenarioNode node, string expectedType)
        {
            return node != null && string.Equals(node.Type, expectedType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
