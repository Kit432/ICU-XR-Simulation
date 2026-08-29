using System;
using System.Globalization;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioPresentationSnapshot
    {
        public bool HasScenario { get; }
        public int HeartRate { get; }
        public int OxygenSaturation { get; }
        public int RespiratoryRate { get; }
        public string BloodPressure { get; }
        public float Temperature { get; }
        public bool AssessmentComplete { get; }
        public bool OxygenAdjusted { get; }
        public bool EscalationComplete { get; }
        public bool MonitorAlert { get; }

        public string HeartRateText => HasScenario ? $"HR: {HeartRate} bpm" : "HR: -- bpm";
        public string OxygenSaturationText => HasScenario ? $"SpO2: {OxygenSaturation}%" : "SpO2: --%";
        public string RespiratoryRateText => HasScenario ? $"RR: {RespiratoryRate} /min" : "RR: -- /min";
        public string BloodPressureText => HasScenario ? $"BP: {BloodPressure} mmHg" : "BP: --/-- mmHg";
        public string TemperatureText => HasScenario
            ? $"Temp: {Temperature.ToString("0.0", CultureInfo.InvariantCulture)} °C"
            : "Temp: --.- °C";
        public string AssessmentStatusText => AssessmentComplete ? "Complete" : "Pending";
        public string OxygenAdjustmentText => OxygenAdjusted ? "Applied" : "Not applied";
        public string EscalationStatusText => EscalationComplete ? "Physician notified" : "Not requested";

        private ScenarioPresentationSnapshot(
            bool hasScenario,
            int heartRate,
            int oxygenSaturation,
            int respiratoryRate,
            string bloodPressure,
            float temperature,
            bool assessmentComplete,
            bool oxygenAdjusted,
            bool escalationComplete,
            bool monitorAlert)
        {
            HasScenario = hasScenario;
            HeartRate = heartRate;
            OxygenSaturation = oxygenSaturation;
            RespiratoryRate = respiratoryRate;
            BloodPressure = bloodPressure;
            Temperature = temperature;
            AssessmentComplete = assessmentComplete;
            OxygenAdjusted = oxygenAdjusted;
            EscalationComplete = escalationComplete;
            MonitorAlert = monitorAlert;
        }

        public static ScenarioPresentationSnapshot FromState(ScenarioState state)
        {
            if (state == null)
            {
                return new ScenarioPresentationSnapshot(
                    false, 0, 0, 0, null, 0f, false, false, false, false);
            }

            return new ScenarioPresentationSnapshot(
                true,
                state.HeartRate,
                state.OxygenSaturation,
                state.RespiratoryRate,
                state.BloodPressure,
                state.Temperature,
                ReadFlag(state, "assessment_complete"),
                ReadFlag(state, "oxygen_adjusted"),
                ReadFlag(state, "escalation_complete"),
                state.MonitorAlert);
        }

        private static bool ReadFlag(ScenarioState state, string flagName)
        {
            return state.Flags != null &&
                   state.Flags.TryGetValue(flagName, out bool value) &&
                   value;
        }
    }

    public sealed class MonitorAlarmPresentationModel
    {
        public bool IsAlarmActive { get; private set; }
        public int ChangeCount { get; private set; }

        public bool ApplyUiEffect(ScenarioUiEffectRequest request)
        {
            if (request == null ||
                !string.Equals(request.EffectType, "ui_visual", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(request.Target, "hs_monitor", StringComparison.Ordinal) ||
                !string.Equals(request.State, "blinking_red", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return SetAlarmActive(request.IsActive);
        }

        public bool ResetFromState(ScenarioState state)
        {
            return SetAlarmActive(state != null && state.MonitorAlert);
        }

        public bool Clear()
        {
            return SetAlarmActive(false);
        }

        private bool SetAlarmActive(bool active)
        {
            if (IsAlarmActive == active)
            {
                return false;
            }

            IsAlarmActive = active;
            ChangeCount++;
            return true;
        }
    }
}
