using ICUSimulation.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonitorView : MonoBehaviour
{
    private ScenarioController scenarioController;
    private GameObject monitorPanel;
    private TMP_Text heartRateText;
    private TMP_Text oxygenSaturationText;
    private TMP_Text respiratoryRateText;
    private TMP_Text bloodPressureText;
    private TMP_Text temperatureText;
    private CanvasGroup alarmGroup;
    private MonitorWaveformGraphic waveform;
    private readonly MonitorAlarmPresentationModel alarmModel = new MonitorAlarmPresentationModel();
    private bool subscribed;

    public void Initialize(ScenarioController controller, GameObject panel)
    {
        Detach();
        scenarioController = controller;
        monitorPanel = panel;
        ResolveTextReferences();
        EnsureAlarmPresentation();
        EnsureWaveform();
        Attach();
        Refresh(scenarioController != null ? scenarioController.CurrentState : null);
        alarmModel.ResetFromState(scenarioController != null ? scenarioController.CurrentState : null);
        UpdateAlarmPresentation();
    }

    private void OnEnable()
    {
        Attach();
        if (scenarioController != null)
        {
            Refresh(scenarioController.CurrentState);
            if (scenarioController.CurrentRunner == null)
            {
                alarmModel.ResetFromState(scenarioController.CurrentState);
            }
        }
    }

    private void OnDisable()
    {
        Detach();
    }

    private void Update()
    {
        UpdateAlarmPresentation();
    }

    private void Attach()
    {
        if (subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged += HandleStateChanged;
        scenarioController.VitalsChanged += HandleVitalsChanged;
        scenarioController.UiEffectRequested += HandleUiEffect;
        subscribed = true;
    }

    private void Detach()
    {
        if (!subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged -= HandleStateChanged;
        scenarioController.VitalsChanged -= HandleVitalsChanged;
        scenarioController.UiEffectRequested -= HandleUiEffect;
        subscribed = false;
    }

    private void HandleStateChanged(ScenarioState state)
    {
        Refresh(state);
        if (scenarioController.CurrentRunner == null)
        {
            alarmModel.ResetFromState(state);
        }
    }

    private void HandleVitalsChanged(ScenarioVitalsChangedEvent change)
    {
        Refresh(scenarioController.CurrentState);
    }

    private void HandleUiEffect(ScenarioUiEffectRequest request)
    {
        alarmModel.ApplyUiEffect(request);
    }

    private void Refresh(ScenarioState state)
    {
        ScenarioPresentationSnapshot snapshot = ScenarioPresentationSnapshot.FromState(state);
        SetText(heartRateText, snapshot.HeartRateText);
        SetText(oxygenSaturationText, snapshot.OxygenSaturationText);
        SetText(respiratoryRateText, snapshot.RespiratoryRateText);
        SetText(bloodPressureText, snapshot.BloodPressureText);
        SetText(temperatureText, snapshot.TemperatureText);
        waveform?.SetHeartRate(snapshot.HasScenario ? snapshot.HeartRate : 60);
    }

    private void ResolveTextReferences()
    {
        if (monitorPanel == null)
        {
            return;
        }

        foreach (TMP_Text text in monitorPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            switch (text.gameObject.name)
            {
                case "HRText": heartRateText = text; break;
                case "SpO2Text": oxygenSaturationText = text; break;
                case "RRText": respiratoryRateText = text; break;
                case "BPText": bloodPressureText = text; break;
                case "TempText": temperatureText = text; break;
            }
        }
    }

    private void EnsureAlarmPresentation()
    {
        if (monitorPanel != null)
            alarmGroup = monitorPanel.transform.Find("AlarmPresentation_Runtime")?.GetComponent<CanvasGroup>();
    }

    private void EnsureWaveform()
    {
        if (monitorPanel != null)
            waveform = monitorPanel.GetComponentInChildren<MonitorWaveformGraphic>(true);
    }

    private void UpdateAlarmPresentation()
    {
        if (alarmGroup == null)
        {
            return;
        }

        alarmGroup.alpha = alarmModel.IsAlarmActive
            ? Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.unscaledTime * 2.4f, 1f))
            : 0f;
        alarmGroup.blocksRaycasts = false;
        alarmGroup.interactable = false;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
