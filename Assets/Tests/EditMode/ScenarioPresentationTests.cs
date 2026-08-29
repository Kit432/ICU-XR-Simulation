using System.IO;
using ICUSimulation.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace ICUSimulation.Tests.EditMode
{
    public sealed class ScenarioPresentationTests
    {
        private ScenarioDefinition definition;

        [SetUp]
        public void SetUp()
        {
            string scenarioPath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Scenarios",
                "icu_scenario_hypoxia_v1.json");
            ScenarioLoadResult loaded = new ScenarioLoader().LoadFromFile(scenarioPath);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            definition = loaded.Definition;
        }

        [Test]
        public void Snapshot_ReceivesInitialVitalsFromScenarioState()
        {
            ScenarioPresentationSnapshot snapshot = SnapshotFromInitialState();

            Assert.That(snapshot.HeartRate, Is.EqualTo(110));
            Assert.That(snapshot.OxygenSaturation, Is.EqualTo(88));
            Assert.That(snapshot.RespiratoryRate, Is.EqualTo(24));
            Assert.That(snapshot.BloodPressure, Is.EqualTo("125/80"));
            Assert.That(snapshot.Temperature, Is.EqualTo(37f));
            Assert.That(snapshot.HeartRateText, Is.EqualTo("HR: 110 bpm"));
        }

        [Test]
        public void Snapshot_ReflectsVitalChangesWithoutDuplicatingState()
        {
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            state.HeartRate = 120;
            state.OxygenSaturation = 85;

            ScenarioPresentationSnapshot snapshot = ScenarioPresentationSnapshot.FromState(state);

            Assert.That(snapshot.HeartRateText, Is.EqualTo("HR: 120 bpm"));
            Assert.That(snapshot.OxygenSaturationText, Is.EqualTo("SpO2: 85%"));
        }

        [Test]
        public void Snapshot_ResetStateRestoresInitialPresentation()
        {
            ScenarioSession session = new ScenarioSession();
            ScenarioState changed = session.LoadScenario(definition);
            changed.HeartRate = 100;
            changed.OxygenSaturation = 94;
            changed.Flags["oxygen_adjusted"] = true;

            ScenarioPresentationSnapshot reset = ScenarioPresentationSnapshot.FromState(session.ResetScenario());

            Assert.That(reset.HeartRate, Is.EqualTo(110));
            Assert.That(reset.OxygenSaturation, Is.EqualTo(88));
            Assert.That(reset.OxygenAdjusted, Is.False);
        }

        [Test]
        public void Snapshot_MapsOxygenAdjustmentForVentilatorPresentation()
        {
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            Assert.That(ScenarioPresentationSnapshot.FromState(state).OxygenAdjustmentText, Is.EqualTo("Not applied"));

            state.Flags["oxygen_adjusted"] = true;

            Assert.That(ScenarioPresentationSnapshot.FromState(state).OxygenAdjustmentText, Is.EqualTo("Applied"));
        }

        [Test]
        public void Snapshot_MapsEscalationForCallButtonPresentation()
        {
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            Assert.That(ScenarioPresentationSnapshot.FromState(state).EscalationStatusText, Is.EqualTo("Not requested"));

            state.Flags["escalation_complete"] = true;

            Assert.That(ScenarioPresentationSnapshot.FromState(state).EscalationStatusText, Is.EqualTo("Physician notified"));
        }

        [Test]
        public void AlarmModel_BlinkingRedEffectActivatesAlarm()
        {
            MonitorAlarmPresentationModel model = new MonitorAlarmPresentationModel();

            bool changed = model.ApplyUiEffect(new ScenarioUiEffectRequest(
                "ui_visual", "hs_monitor", "blinking_red", "rule", true));

            Assert.That(changed, Is.True);
            Assert.That(model.IsAlarmActive, Is.True);
        }

        [Test]
        public void AlarmModel_DeactivationAndResetClearAlarmWhenStateIsNormal()
        {
            MonitorAlarmPresentationModel model = new MonitorAlarmPresentationModel();
            model.ApplyUiEffect(new ScenarioUiEffectRequest(
                "ui_visual", "hs_monitor", "blinking_red", "rule", true));

            model.ApplyUiEffect(new ScenarioUiEffectRequest(
                "ui_visual", "hs_monitor", "blinking_red", "rule", false));
            Assert.That(model.IsAlarmActive, Is.False);

            ScenarioState normalState = ScenarioState.CreateFromDefinition(definition);
            normalState.MonitorAlert = false;
            model.ResetFromState(normalState);
            Assert.That(model.IsAlarmActive, Is.False);
        }

        [Test]
        public void AlarmModel_DuplicateEffectDoesNotProduceDuplicateStateChange()
        {
            MonitorAlarmPresentationModel model = new MonitorAlarmPresentationModel();
            ScenarioUiEffectRequest activation = new ScenarioUiEffectRequest(
                "ui_visual", "hs_monitor", "blinking_red", "rule", true);

            Assert.That(model.ApplyUiEffect(activation), Is.True);
            Assert.That(model.ApplyUiEffect(activation), Is.False);
            Assert.That(model.ChangeCount, Is.EqualTo(1));
        }

        private ScenarioPresentationSnapshot SnapshotFromInitialState()
        {
            return ScenarioPresentationSnapshot.FromState(
                ScenarioState.CreateFromDefinition(definition));
        }
    }
}
