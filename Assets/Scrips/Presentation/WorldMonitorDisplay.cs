using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds scenario values to the editable bedside-monitor world-space display prefab.</summary>
public sealed class WorldMonitorDisplay : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;
    public Text heartRate;
    public Text oxygenSaturation;
    public Text respiratoryRate;
    public Text bloodPressure;
    public Text temperature;
    public Text alarmStatus;
    public Image alarmBackground;
    private readonly MonitorAlarmPresentationModel alarmModel = new MonitorAlarmPresentationModel();

    private void Awake()
    {
        if (scenarioController == null) scenarioController = FindAnyObjectByType<ScenarioController>();
    }
    private void OnEnable()
    {
        if (scenarioController == null) return;
        scenarioController.StateChanged += Refresh;
        scenarioController.VitalsChanged += RefreshVitals;
        scenarioController.UiEffectRequested += HandleUiEffect;
        alarmModel.ResetFromState(scenarioController.CurrentState);
        Refresh(scenarioController.CurrentState);
    }
    private void Refresh(ScenarioState state)
    {
        ScenarioPresentationSnapshot snapshot = ScenarioPresentationSnapshot.FromState(state);
        heartRate.text = snapshot.HasScenario ? snapshot.HeartRate + " <size=23>bpm</size>" : "--";
        oxygenSaturation.text = snapshot.HasScenario ? snapshot.OxygenSaturation + "%" : "--";
        respiratoryRate.text = snapshot.HasScenario ? snapshot.RespiratoryRate + " <size=23>/min</size>" : "--";
        bloodPressure.text = snapshot.HasScenario ? snapshot.BloodPressure : "--/--";
        temperature.text = snapshot.HasScenario ? snapshot.Temperature.ToString("0.0") + " °C" : "--.- °C";
        if (scenarioController.CurrentRunner == null) alarmModel.ResetFromState(state);
        UpdateAlarm();
    }
    private void RefreshVitals(ScenarioVitalsChangedEvent change) => Refresh(scenarioController.CurrentState);
    private void HandleUiEffect(ScenarioUiEffectRequest request)
    {
        alarmModel.ApplyUiEffect(request);
        UpdateAlarm();
    }
    private void UpdateAlarm()
    {
        bool loaded = scenarioController.CurrentState != null;
        alarmStatus.text = !loaded ? "LOAD A SCENARIO TO BEGIN" : alarmModel.IsAlarmActive ? "ALARM  |  CHECK PATIENT" : "MONITORING  |  NO ACTIVE ALARM";
        alarmBackground.color = alarmModel.IsAlarmActive ? new Color(0.55f, 0.06f, 0.06f, 1) : new Color(0.025f, 0.23f, 0.22f, 1);
        oxygenSaturation.color = alarmModel.IsAlarmActive ? new Color(1f, 0.43f, 0.32f) : new Color(0.20f, 0.91f, 1f);
    }
    private void OnDisable()
    {
        if (scenarioController == null) return;
        scenarioController.StateChanged -= Refresh;
        scenarioController.VitalsChanged -= RefreshVitals;
        scenarioController.UiEffectRequested -= HandleUiEffect;
    }
}
