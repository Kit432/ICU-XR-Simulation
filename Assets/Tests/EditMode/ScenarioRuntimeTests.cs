using System.IO;
using ICUSimulation.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace ICUSimulation.Tests.EditMode
{
    public sealed class ScenarioRuntimeTests
    {
        private ScenarioDefinition definition;

        [SetUp]
        public void SetUp()
        {
            string samplePath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Scenarios",
                "icu_scenario_hypoxia_v1.json");
            ScenarioLoadResult loaded = new ScenarioLoader().LoadFromFile(samplePath);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            definition = loaded.Definition;
        }

        [Test]
        public void Start_EntersFirstNodeAndRaisesEvent()
        {
            ScenarioRunner runner = CreateRunner();
            ScenarioNode entered = null;
            runner.NodeEntered += node => entered = node;

            Assert.That(runner.Start(), Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n1_start"));
            Assert.That(runner.State.CurrentNodeId, Is.EqualTo("n1_start"));
            Assert.That(entered?.Id, Is.EqualTo("n1_start"));
        }

        [Test]
        public void MessageContinue_TransitionsToDecision()
        {
            ScenarioRunner runner = CreateStartedRunner();

            Assert.That(runner.ContinueMessage(), Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n2_initial_decision"));
        }

        [Test]
        public void MatchingHotspot_SelectsExpectedOption()
        {
            ScenarioRunner runner = ReachInitialDecision();
            ScenarioOptionSelectedEvent selected = null;
            runner.OptionSelected += value => selected = value;

            Assert.That(runner.TryHandleHotspotInteraction("hs_patient"), Is.True);
            Assert.That(selected?.Option.Id, Is.EqualTo("opt_assess"));
            Assert.That(selected?.HotspotId, Is.EqualTo("hs_patient"));
        }

        [Test]
        public void NonMatchingHotspot_DoesNotTransition()
        {
            ScenarioRunner runner = ReachInitialDecision();

            Assert.That(runner.TryHandleHotspotInteraction("hs_ehr"), Is.False);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n2_initial_decision"));
            Assert.That(runner.IsTimeoutActive, Is.True);
        }

        [Test]
        public void AssessOption_AppliesEffectsAndTransitions()
        {
            ScenarioRunner runner = ReachInitialDecision();

            runner.TryHandleHotspotInteraction("hs_patient");

            Assert.That(runner.State.CurrentScore, Is.EqualTo(110));
            Assert.That(runner.State.Flags["assessment_complete"], Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n3_intervention"));
        }

        [Test]
        public void CheckMonitorOption_AppliesScoreAndTransitions()
        {
            ScenarioRunner runner = ReachInitialDecision();

            runner.TryHandleHotspotInteraction("hs_monitor");

            Assert.That(runner.State.CurrentScore, Is.EqualTo(105));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n3_intervention"));
        }

        [Test]
        public void IncreaseOxygenOption_AppliesAllEffects()
        {
            ScenarioRunner runner = ReachInterventionDecision();

            runner.TryHandleHotspotInteraction("hs_ventilator");

            Assert.That(runner.State.Flags["oxygen_adjusted"], Is.True);
            Assert.That(runner.State.OxygenSaturation, Is.EqualTo(94));
            Assert.That(runner.State.HeartRate, Is.EqualTo(100));
            Assert.That(runner.State.CurrentScore, Is.EqualTo(125));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n4_gate_documentation_1"));
        }

        [Test]
        public void DoNothingOption_AppliesDeteriorationEffects()
        {
            ScenarioRunner runner = ReachInterventionDecision();

            runner.TryHandleHotspotInteraction("hs_patient");

            Assert.That(runner.State.OxygenSaturation, Is.EqualTo(80));
            Assert.That(runner.State.HeartRate, Is.EqualTo(130));
            Assert.That(runner.State.CurrentScore, Is.EqualTo(85));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n4_gate_documentation_1"));
        }

        [Test]
        public void Timeout_AppliesEffectsAndTransitionsOnce()
        {
            ScenarioRunner runner = ReachInitialDecision();
            int timeoutCount = 0;
            runner.TimeoutTriggered += value => timeoutCount++;

            runner.Tick(30f);
            runner.Tick(30f);

            Assert.That(timeoutCount, Is.EqualTo(1));
            Assert.That(runner.State.OxygenSaturation, Is.EqualTo(85));
            Assert.That(runner.State.HeartRate, Is.EqualTo(120));
            Assert.That(runner.State.CurrentScore, Is.EqualTo(90));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n3_intervention"));
        }

        [Test]
        public void Timeout_DoesNotFireAfterOptionSelected()
        {
            ScenarioRunner runner = ReachInitialDecision();
            int timeoutCount = 0;
            runner.TimeoutTriggered += value => timeoutCount++;

            runner.TryHandleHotspotInteraction("hs_monitor");
            runner.Tick(60f);

            Assert.That(timeoutCount, Is.Zero);
            Assert.That(runner.State.CurrentScore, Is.EqualTo(105));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n3_intervention"));
        }

        [Test]
        public void GlobalHypoxiaRule_EvaluatesFromJson()
        {
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            RuleEvaluator evaluator = new RuleEvaluator();

            Assert.That(evaluator.EvaluateGlobalRule(definition.Rules.GlobalRules[0], state), Is.True);
            state.OxygenSaturation = 90;
            Assert.That(evaluator.EvaluateGlobalRule(definition.Rules.GlobalRules[0], state), Is.False);
        }

        [Test]
        public void GlobalRuleActivation_IsEdgeTriggeredAndDoesNotSpam()
        {
            ScenarioRunner runner = CreateRunner();
            int activationCount = 0;
            runner.GlobalRuleActivated += value => activationCount++;

            runner.Start();
            runner.Tick(0.1f);
            runner.Tick(0.1f);
            runner.EvaluateGlobalRules();

            Assert.That(activationCount, Is.EqualTo(1));
        }

        [Test]
        public void GlobalRuleActivation_RearmsAfterConditionClears()
        {
            ScenarioRunner runner = CreateRunner();
            int activationCount = 0;
            runner.GlobalRuleActivated += value => activationCount++;

            runner.Start();
            runner.State.OxygenSaturation = 94;
            runner.EvaluateGlobalRules();
            runner.State.OxygenSaturation = 88;
            runner.EvaluateGlobalRules();

            Assert.That(activationCount, Is.EqualTo(2));
        }

        [Test]
        public void GlobalRuleDeactivation_EmitsInactiveUiVisualRequest()
        {
            ScenarioRunner runner = CreateRunner();
            ScenarioUiEffectRequest deactivation = null;
            runner.UiEffectRequested += request =>
            {
                if (!request.IsActive)
                {
                    deactivation = request;
                }
            };

            runner.Start();
            runner.State.OxygenSaturation = 94;
            runner.EvaluateGlobalRules();

            Assert.That(deactivation, Is.Not.Null);
            Assert.That(deactivation.Target, Is.EqualTo("hs_monitor"));
            Assert.That(deactivation.State, Is.EqualTo("blinking_red"));
            Assert.That(deactivation.IsActive, Is.False);
        }

        [Test]
        public void UnsatisfiedGate_RemainsBlocked()
        {
            ScenarioRunner runner = ReachInterventionDecision();
            int blockedCount = 0;
            runner.GateBlocked += value => blockedCount++;

            runner.TryHandleHotspotInteraction("hs_ventilator");

            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n4_gate_documentation_1"));
            Assert.That(runner.IsWaitingAtGate, Is.True);
            Assert.That(blockedCount, Is.EqualTo(1));
            Assert.That(runner.State.Flags["documentation_1_complete"], Is.False);
        }

        [Test]
        public void DebugBypassedGate_AppliesPassEffectsAndTransitions()
        {
            ScenarioRunner runner = ReachFirstGate();
            int scoreBeforeGate = runner.State.CurrentScore;
            ScenarioGateEvent passed = null;
            runner.GatePassed += value => passed = value;

            Assert.That(runner.DebugBypassCurrentGate(), Is.True);

            Assert.That(passed?.WasDebugBypassed, Is.True);
            Assert.That(runner.State.Flags["documentation_1_complete"], Is.True);
            Assert.That(runner.State.CurrentScore, Is.EqualTo(scoreBeforeGate + 10));
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n5_reassessment"));
        }

        [Test]
        public void SatisfiedGate_AutomaticallyPassesThroughResolver()
        {
            ScenarioRunner runner = ReachInterventionDecision();
            runner.GateRequirementResolver = (formId, fieldId) => true;

            runner.TryHandleHotspotInteraction("hs_ventilator");

            Assert.That(runner.State.Flags["documentation_1_complete"], Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n5_reassessment"));
        }

        [Test]
        public void EndNode_MarksScenarioComplete()
        {
            ScenarioRunner runner = ReachFirstGate();
            runner.DebugBypassCurrentGate();
            runner.ContinueMessage();
            runner.TryHandleHotspotInteraction("hs_monitor");

            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n8_end_scenario"));
            Assert.That(runner.IsCompleted, Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.IsTimeoutActive, Is.False);
        }

        [Test]
        public void CallButtonPath_ReachesSecondGateAndCanCompleteThroughDebugBypass()
        {
            ScenarioRunner runner = ReachFirstGate();
            runner.DebugBypassCurrentGate();
            runner.ContinueMessage();

            Assert.That(runner.TryHandleHotspotInteraction("hs_call"), Is.True);
            Assert.That(runner.State.Flags["escalation_complete"], Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n7_gate_documentation_2"));
            Assert.That(runner.IsWaitingAtGate, Is.True);

            Assert.That(runner.DebugBypassCurrentGate(), Is.True);
            Assert.That(runner.State.Flags["documentation_2_complete"], Is.True);
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n8_end_scenario"));
            Assert.That(runner.IsCompleted, Is.True);
        }

        [Test]
        public void StopAndSessionReset_CancelProgressionAndRestoreInitialState()
        {
            ScenarioSession session = new ScenarioSession();
            ScenarioState state = session.LoadScenario(definition);
            ScenarioRunner runner = new ScenarioRunner(definition, state);
            runner.Start();
            runner.ContinueMessage();
            runner.Stop();
            runner.Tick(60f);

            ScenarioState reset = session.ResetScenario();

            Assert.That(state.CurrentScore, Is.EqualTo(100));
            Assert.That(state.OxygenSaturation, Is.EqualTo(88));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.IsTimeoutActive, Is.False);
            Assert.That(reset.CurrentScore, Is.EqualTo(100));
            Assert.That(reset.OxygenSaturation, Is.EqualTo(88));
            Assert.That(reset.CurrentNodeId, Is.EqualTo("n1_start"));
        }

        private ScenarioRunner CreateRunner()
        {
            return new ScenarioRunner(definition, ScenarioState.CreateFromDefinition(definition));
        }

        private ScenarioRunner CreateStartedRunner()
        {
            ScenarioRunner runner = CreateRunner();
            runner.Start();
            return runner;
        }

        private ScenarioRunner ReachInitialDecision()
        {
            ScenarioRunner runner = CreateStartedRunner();
            runner.ContinueMessage();
            return runner;
        }

        private ScenarioRunner ReachInterventionDecision()
        {
            ScenarioRunner runner = ReachInitialDecision();
            runner.TryHandleHotspotInteraction("hs_monitor");
            return runner;
        }

        private ScenarioRunner ReachFirstGate()
        {
            ScenarioRunner runner = ReachInterventionDecision();
            runner.TryHandleHotspotInteraction("hs_ventilator");
            return runner;
        }
    }
}
