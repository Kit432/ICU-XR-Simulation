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
        if (monitorPanel == null || alarmGroup != null)
        {
            return;
        }

        Transform existing = monitorPanel.transform.Find("AlarmPresentation_Runtime");
        GameObject alarmRoot = existing != null
            ? existing.gameObject
            : new GameObject("AlarmPresentation_Runtime", typeof(RectTransform), typeof(CanvasGroup));
        alarmRoot.transform.SetParent(monitorPanel.transform, false);
        RectTransform rootRect = alarmRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        alarmGroup = alarmRoot.GetComponent<CanvasGroup>();

        if (existing == null)
        {
            CreateBorder(alarmRoot.transform, "AlarmTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 6f));
            CreateBorder(alarmRoot.transform, "AlarmBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f));
            CreateBorder(alarmRoot.transform, "AlarmLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 0f));
            CreateBorder(alarmRoot.transform, "AlarmRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f));
            CreateAlarmLabel(alarmRoot.transform);
        }
    }

    private void EnsureWaveform()
    {
        if (monitorPanel == null || waveform != null)
        {
            return;
        }

        Transform existing = monitorPanel.transform.Find("Waveform_Runtime");
        GameObject waveformObject = existing != null
            ? existing.gameObject
            : new GameObject("Waveform_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(MonitorWaveformGraphic));
        waveformObject.transform.SetParent(monitorPanel.transform, false);
        RectTransform rect = waveformObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -145f);
        rect.sizeDelta = new Vector2(470f, 80f);
        waveform = waveformObject.GetComponent<MonitorWaveformGraphic>();
        waveform.color = new Color(0.15f, 1f, 0.42f, 1f);
        waveform.raycastTarget = false;
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

    private static void CreateBorder(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta)
    {
        GameObject border = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        border.transform.SetParent(parent, false);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;
        Image image = border.GetComponent<Image>();
        image.color = new Color(1f, 0.06f, 0.04f, 0.95f);
        image.raycastTarget = false;
    }

    private static void CreateAlarmLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("AlarmLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -16f);
        rect.sizeDelta = new Vector2(260f, 44f);
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "ALARM — CHECK MONITOR";
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
