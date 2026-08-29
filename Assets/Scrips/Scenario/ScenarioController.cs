using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioController : MonoBehaviour
    {
        private readonly ScenarioSession session = new ScenarioSession();
        private ScenarioLoader loader;
        private List<ScenarioDescriptor> availableScenarios = new List<ScenarioDescriptor>();
        private ScenarioRunner runner;

        public event Action AvailableScenariosChanged;
        public event Action<ScenarioState> StateChanged;
        public event Action<string> ErrorOccurred;
        public event Action<ScenarioRunner> ScenarioStarted;
        public event Action<ScenarioNode> NodeEntered;
        public event Action<ScenarioOptionSelectedEvent> OptionSelected;
        public event Action<ScenarioVitalsChangedEvent> VitalsChanged;
        public event Action<ScenarioScoreChangedEvent> ScoreChanged;
        public event Action<ScenarioFlagChangedEvent> FlagChanged;
        public event Action<ScenarioTimeoutEvent> TimeoutTriggered;
        public event Action<ScenarioGlobalRuleEvent> GlobalRuleActivated;
        public event Action<ScenarioGateEvent> GateBlocked;
        public event Action<ScenarioGateEvent> GatePassed;
        public event Action<ScenarioState> ScenarioCompleted;
        public event Action<ScenarioFeedbackRequest> FeedbackRequested;
        public event Action<ScenarioUiEffectRequest> UiEffectRequested;
        public event Action<string> RuntimeWarningRaised;

        public IReadOnlyList<ScenarioDescriptor> AvailableScenarios => availableScenarios;
        public ScenarioDefinition ActiveDefinition => session.Definition;
        public ScenarioState CurrentState => session.State;
        public ScenarioRunner CurrentRunner => runner;
        public string LastError { get; private set; }
        public Func<string, string, bool> GateRequirementResolver { get; set; }

        private void Awake()
        {
            loader = new ScenarioLoader();
            RefreshScenarios();
        }

        private void Update()
        {
            runner?.Tick(Time.deltaTime);
        }

        public void RefreshScenarios()
        {
            if (loader == null)
            {
                loader = new ScenarioLoader();
            }

            ScenarioDiscoveryResult discovery = loader.DiscoverScenarios();
            availableScenarios = discovery.Scenarios;
            LastError = discovery.Errors.Count > 0
                ? string.Join(Environment.NewLine, discovery.Errors)
                : null;

            AvailableScenariosChanged?.Invoke();

            if (!string.IsNullOrEmpty(LastError))
            {
                Debug.LogWarning($"Some scenarios could not be discovered:{Environment.NewLine}{LastError}", this);
                ErrorOccurred?.Invoke(LastError);
            }
        }

        public bool LoadScenario(int scenarioIndex)
        {
            if (scenarioIndex < 0 || scenarioIndex >= availableScenarios.Count)
            {
                ReportError($"Scenario index {scenarioIndex} is outside the available scenario list.");
                return false;
            }

            return LoadScenario(availableScenarios[scenarioIndex]);
        }

        public bool LoadScenario(ScenarioDescriptor descriptor)
        {
            if (descriptor == null)
            {
                ReportError("A scenario must be selected before loading.");
                return false;
            }

            StopRuntime();
            ScenarioLoadResult loaded = loader.LoadFromFile(descriptor.FilePath);
            if (!loaded.IsSuccess)
            {
                ReportError($"Could not load '{descriptor.Title ?? descriptor.FileName}': {loaded.Error}");
                return false;
            }

            LastError = null;
            ScenarioState state = session.LoadScenario(loaded.Definition);
            StateChanged?.Invoke(state);
            return true;
        }

        public bool StartScenario()
        {
            if (session.Definition == null)
            {
                ReportError("Load a scenario before starting it.");
                return false;
            }

            StopRuntime();
            ScenarioState freshState = session.ResetScenario();
            StateChanged?.Invoke(freshState);

            runner = new ScenarioRunner(session.Definition, freshState)
            {
                GateRequirementResolver = GateRequirementResolver
            };
            SubscribeToRunner(runner);
            LastError = null;
            return runner.Start();
        }

        public bool ContinueCurrentMessage()
        {
            return runner != null && runner.ContinueMessage();
        }

        public bool HandleHotspotInteraction(string hotspotId)
        {
            return runner != null && runner.TryHandleHotspotInteraction(hotspotId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugBypassCurrentGate()
        {
            return runner != null && runner.DebugBypassCurrentGate();
        }
#endif

        public bool ResetScenario()
        {
            StopRuntime();
            ScenarioState state = session.ResetScenario();
            if (state == null)
            {
                ReportError("No scenario is loaded, so there is nothing to reset.");
                return false;
            }

            LastError = null;
            StateChanged?.Invoke(state);
            return true;
        }

        public void StopRuntime()
        {
            if (runner == null)
            {
                return;
            }

            UnsubscribeFromRunner(runner);
            runner.Stop();
            runner = null;
        }

        private void SubscribeToRunner(ScenarioRunner activeRunner)
        {
            activeRunner.ScenarioStarted += ForwardScenarioStarted;
            activeRunner.NodeEntered += ForwardNodeEntered;
            activeRunner.OptionSelected += ForwardOptionSelected;
            activeRunner.StateChanged += ForwardStateChanged;
            activeRunner.VitalsChanged += ForwardVitalsChanged;
            activeRunner.ScoreChanged += ForwardScoreChanged;
            activeRunner.FlagChanged += ForwardFlagChanged;
            activeRunner.TimeoutTriggered += ForwardTimeoutTriggered;
            activeRunner.GlobalRuleActivated += ForwardGlobalRuleActivated;
            activeRunner.GateBlocked += ForwardGateBlocked;
            activeRunner.GatePassed += ForwardGatePassed;
            activeRunner.ScenarioCompleted += ForwardScenarioCompleted;
            activeRunner.FeedbackRequested += ForwardFeedbackRequested;
            activeRunner.UiEffectRequested += ForwardUiEffectRequested;
            activeRunner.WarningRaised += ForwardRuntimeWarning;
        }

        private void UnsubscribeFromRunner(ScenarioRunner activeRunner)
        {
            activeRunner.ScenarioStarted -= ForwardScenarioStarted;
            activeRunner.NodeEntered -= ForwardNodeEntered;
            activeRunner.OptionSelected -= ForwardOptionSelected;
            activeRunner.StateChanged -= ForwardStateChanged;
            activeRunner.VitalsChanged -= ForwardVitalsChanged;
            activeRunner.ScoreChanged -= ForwardScoreChanged;
            activeRunner.FlagChanged -= ForwardFlagChanged;
            activeRunner.TimeoutTriggered -= ForwardTimeoutTriggered;
            activeRunner.GlobalRuleActivated -= ForwardGlobalRuleActivated;
            activeRunner.GateBlocked -= ForwardGateBlocked;
            activeRunner.GatePassed -= ForwardGatePassed;
            activeRunner.ScenarioCompleted -= ForwardScenarioCompleted;
            activeRunner.FeedbackRequested -= ForwardFeedbackRequested;
            activeRunner.UiEffectRequested -= ForwardUiEffectRequested;
            activeRunner.WarningRaised -= ForwardRuntimeWarning;
        }

        private void ForwardScenarioStarted(ScenarioRunner activeRunner) => ScenarioStarted?.Invoke(activeRunner);
        private void ForwardNodeEntered(ScenarioNode node) => NodeEntered?.Invoke(node);
        private void ForwardOptionSelected(ScenarioOptionSelectedEvent selected) => OptionSelected?.Invoke(selected);
        private void ForwardStateChanged(ScenarioState state) => StateChanged?.Invoke(state);
        private void ForwardVitalsChanged(ScenarioVitalsChangedEvent change) => VitalsChanged?.Invoke(change);
        private void ForwardScoreChanged(ScenarioScoreChangedEvent change) => ScoreChanged?.Invoke(change);
        private void ForwardFlagChanged(ScenarioFlagChangedEvent change) => FlagChanged?.Invoke(change);
        private void ForwardTimeoutTriggered(ScenarioTimeoutEvent timeout) => TimeoutTriggered?.Invoke(timeout);
        private void ForwardGlobalRuleActivated(ScenarioGlobalRuleEvent rule) => GlobalRuleActivated?.Invoke(rule);
        private void ForwardGateBlocked(ScenarioGateEvent gate) => GateBlocked?.Invoke(gate);
        private void ForwardGatePassed(ScenarioGateEvent gate) => GatePassed?.Invoke(gate);
        private void ForwardScenarioCompleted(ScenarioState state) => ScenarioCompleted?.Invoke(state);
        private void ForwardFeedbackRequested(ScenarioFeedbackRequest request) => FeedbackRequested?.Invoke(request);
        private void ForwardUiEffectRequested(ScenarioUiEffectRequest request) => UiEffectRequested?.Invoke(request);

        private void ForwardRuntimeWarning(string warning)
        {
            Debug.LogWarning(warning, this);
            RuntimeWarningRaised?.Invoke(warning);
        }

        private void ReportError(string error)
        {
            LastError = error;
            Debug.LogWarning(error, this);
            ErrorOccurred?.Invoke(error);
        }

        private void OnDestroy()
        {
            StopRuntime();
        }
    }
}
