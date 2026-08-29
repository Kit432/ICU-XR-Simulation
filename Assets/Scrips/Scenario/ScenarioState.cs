using System;
using System.Collections.Generic;
using System.Linq;

namespace ICUSimulation.Scenarios
{
    [Serializable]
    public sealed class ScenarioState
    {
        public string ActiveScenarioId { get; set; }
        public string CurrentNodeId { get; set; }
        public float ElapsedTime { get; set; }
        public int CurrentScore { get; set; }
        public int HeartRate { get; set; }
        public int OxygenSaturation { get; set; }
        public int RespiratoryRate { get; set; }
        public string BloodPressure { get; set; }
        public float Temperature { get; set; }
        public Dictionary<string, bool> Flags { get; set; } = new Dictionary<string, bool>();
        public List<string> ActiveHotspots { get; set; } = new List<string>();
        public bool MonitorAlert { get; set; }

        public static ScenarioState CreateFromDefinition(ScenarioDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ScenarioValidationResult validation = ScenarioValidator.Validate(definition);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    $"Cannot create runtime state from an invalid scenario:{Environment.NewLine}{validation.FormatErrors()}",
                    nameof(definition));
            }

            ScenarioInitialState initial = definition.InitialState;
            ScenarioVitals vitals = initial.Vitals;
            ScenarioInitialUiState ui = initial.Ui;

            return new ScenarioState
            {
                ActiveScenarioId = definition.Metadata.Id,
                CurrentNodeId = definition.Nodes.First().Id,
                ElapsedTime = initial.TimeElapsed,
                CurrentScore = initial.CurrentScore,
                HeartRate = vitals.HeartRate,
                OxygenSaturation = vitals.OxygenSaturation,
                RespiratoryRate = vitals.RespiratoryRate,
                BloodPressure = vitals.BloodPressure,
                Temperature = vitals.Temperature,
                Flags = initial.Flags != null
                    ? new Dictionary<string, bool>(initial.Flags, StringComparer.Ordinal)
                    : new Dictionary<string, bool>(StringComparer.Ordinal),
                ActiveHotspots = ui?.ActiveHotspots != null
                    ? new List<string>(ui.ActiveHotspots)
                    : new List<string>(),
                MonitorAlert = ui != null && ui.MonitorAlert
            };
        }

        public ScenarioState DeepCopy()
        {
            return new ScenarioState
            {
                ActiveScenarioId = ActiveScenarioId,
                CurrentNodeId = CurrentNodeId,
                ElapsedTime = ElapsedTime,
                CurrentScore = CurrentScore,
                HeartRate = HeartRate,
                OxygenSaturation = OxygenSaturation,
                RespiratoryRate = RespiratoryRate,
                BloodPressure = BloodPressure,
                Temperature = Temperature,
                Flags = Flags != null
                    ? new Dictionary<string, bool>(Flags, StringComparer.Ordinal)
                    : new Dictionary<string, bool>(StringComparer.Ordinal),
                ActiveHotspots = ActiveHotspots != null
                    ? new List<string>(ActiveHotspots)
                    : new List<string>(),
                MonitorAlert = MonitorAlert
            };
        }
    }

    public sealed class ScenarioSession
    {
        private ScenarioState initialStateSnapshot;

        public ScenarioDefinition Definition { get; private set; }
        public ScenarioState State { get; private set; }

        public ScenarioState LoadScenario(ScenarioDefinition definition)
        {
            ScenarioState initialState = ScenarioState.CreateFromDefinition(definition);
            Definition = definition;
            initialStateSnapshot = initialState.DeepCopy();
            State = initialState.DeepCopy();
            return State;
        }

        public ScenarioState ResetScenario()
        {
            if (initialStateSnapshot == null)
            {
                return null;
            }

            State = initialStateSnapshot.DeepCopy();
            return State;
        }
    }
}
