using System;
using System.Collections.Generic;
using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ScenarioDebugPanel : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;

    private GameObject panel;
    private Dropdown scenarioDropdown;
    private Text stateText;
    private Text errorText;
    private Font runtimeFont;

    private void Awake()
    {
        if (scenarioController == null)
        {
            scenarioController = FindAnyObjectByType<ScenarioController>();
        }

        if (scenarioController == null)
        {
            scenarioController = gameObject.AddComponent<ScenarioController>();
        }

        EnsureRuntimeComponent<ScenarioHotspotCoordinator>();
        EnsureRuntimeComponent<FeedbackController>();
        EnsureRuntimeComponent<ScenarioHudController>();
        EnsureRuntimeComponent<ClinicalPresentationCoordinator>();

        scenarioController.AvailableScenariosChanged += RefreshScenarioList;
        scenarioController.StateChanged += DisplayState;
        scenarioController.ScenarioCompleted += DisplayState;
        scenarioController.ErrorOccurred += DisplayError;
    }

    private void Start()
    {
        BuildTemporaryPanel();
        RefreshScenarioList();
        DisplayState(scenarioController.CurrentState);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.f2Key.wasPressedThisFrame)
        {
            return;
        }

        if (panel.activeSelf)
        {
            UIFlowController.Instance?.CloseActivePanel();
            return;
        }

        scenarioController.RefreshScenarios();
        UIFlowController.Instance?.OpenPanel(panel);
    }

    private void BuildTemporaryPanel()
    {
        if (panel != null)
        {
            return;
        }

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        DefaultControls.Resources controls = new DefaultControls.Resources();

        panel = new GameObject("ScenarioDebugPanel_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 520f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.075f, 0.97f);

        CreateLabel(panel.transform, "Phase 3 Scenario Runtime Debugger", 26, TextAnchor.MiddleCenter,
            new Vector2(20f, -18f), new Vector2(580f, 42f));
        CreateLabel(panel.transform, "Scenario", 18, TextAnchor.MiddleLeft,
            new Vector2(30f, -75f), new Vector2(120f, 34f));

        GameObject dropdownObject = DefaultControls.CreateDropdown(controls);
        dropdownObject.name = "ScenarioDropdown";
        dropdownObject.transform.SetParent(panel.transform, false);
        SetRect(dropdownObject.GetComponent<RectTransform>(), new Vector2(150f, -75f), new Vector2(430f, 38f));
        scenarioDropdown = dropdownObject.GetComponent<Dropdown>();
        ApplyFontRecursively(dropdownObject);

        Button loadButton = CreateButton(panel.transform, controls, "Load", new Vector2(30f, -130f));
        Button resetButton = CreateButton(panel.transform, controls, "Reset", new Vector2(175f, -130f));
        Button startButton = CreateButton(panel.transform, controls, "Start", new Vector2(320f, -130f));
        Button closeButton = CreateButton(panel.transform, controls, "Close", new Vector2(465f, -130f));
        loadButton.onClick.AddListener(LoadSelectedScenario);
        resetButton.onClick.AddListener(ResetScenario);
        startButton.onClick.AddListener(StartScenario);
        closeButton.onClick.AddListener(() => UIFlowController.Instance?.CloseActivePanel());

        CreateLabel(panel.transform, "Current loaded state", 19, TextAnchor.MiddleLeft,
            new Vector2(30f, -195f), new Vector2(560f, 34f));
        stateText = CreateLabel(panel.transform, string.Empty, 18, TextAnchor.UpperLeft,
            new Vector2(30f, -235f), new Vector2(560f, 210f));
        errorText = CreateLabel(panel.transform, string.Empty, 15, TextAnchor.UpperLeft,
            new Vector2(30f, -450f), new Vector2(560f, 52f));
        errorText.color = new Color(1f, 0.45f, 0.4f);

        panel.SetActive(false);
    }

    private void RefreshScenarioList()
    {
        if (scenarioDropdown == null)
        {
            return;
        }

        List<string> titles = new List<string>();
        foreach (ScenarioDescriptor scenario in scenarioController.AvailableScenarios)
        {
            titles.Add(string.IsNullOrWhiteSpace(scenario.Title) ? scenario.FileName : scenario.Title);
        }

        scenarioDropdown.ClearOptions();
        scenarioDropdown.AddOptions(titles.Count > 0 ? titles : new List<string> { "No valid scenarios found" });
        scenarioDropdown.interactable = titles.Count > 0;
        DisplayError(scenarioController.LastError);
    }

    private void LoadSelectedScenario()
    {
        if (scenarioController.AvailableScenarios.Count == 0)
        {
            DisplayError("No valid scenario is available to load.");
            return;
        }

        scenarioController.LoadScenario(scenarioDropdown.value);
    }

    private void ResetScenario()
    {
        scenarioController.ResetScenario();
    }

    private void StartScenario()
    {
        if (scenarioController.StartScenario())
        {
            UIFlowController.Instance?.CloseActivePanel();
        }
    }

    private void DisplayState(ScenarioState state)
    {
        if (stateText == null)
        {
            return;
        }

        string runtimeStatus = scenarioController.CurrentRunner == null
            ? "Loaded / stopped"
            : scenarioController.CurrentRunner.IsCompleted
                ? "Complete"
                : "Running";

        stateText.text = state == null
            ? "No scenario loaded."
            : string.Format(
                "Scenario: {0}\nRuntime: {1}\nNode: {2}\nScore: {3}\nHR: {4}\nSpO2: {5}%\nRR: {6}\nBP: {7}\nTemperature: {8:0.0} °C\nMonitor alert: {9}",
                state.ActiveScenarioId,
                runtimeStatus,
                state.CurrentNodeId,
                state.CurrentScore,
                state.HeartRate,
                state.OxygenSaturation,
                state.RespiratoryRate,
                state.BloodPressure,
                state.Temperature,
                state.MonitorAlert);

        DisplayError(null);
    }

    private void DisplayError(string error)
    {
        if (errorText != null)
        {
            errorText.text = error ?? string.Empty;
        }
    }

    private Button CreateButton(Transform parent, DefaultControls.Resources controls, string label, Vector2 position)
    {
        GameObject buttonObject = DefaultControls.CreateButton(controls);
        buttonObject.name = label + "Button";
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, new Vector2(140f, 42f));
        Text text = buttonObject.GetComponentInChildren<Text>();
        text.text = label;
        text.font = runtimeFont;
        text.fontSize = 18;
        return buttonObject.GetComponent<Button>();
    }

    private Text CreateLabel(
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size)
    {
        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        SetRect(labelObject.GetComponent<RectTransform>(), position, size);
        Text label = labelObject.GetComponent<Text>();
        label.text = value;
        label.font = runtimeFont;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private void ApplyFontRecursively(GameObject root)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            text.font = runtimeFont;
            text.fontSize = 16;
        }
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private T EnsureRuntimeComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void OnGUI()
    {
        if (panel != null && !panel.activeSelf)
        {
            GUI.Box(new Rect(Screen.width - 210f, 12f, 198f, 30f), "F2: Scenario data debugger");
        }
    }

    private void OnDestroy()
    {
        if (scenarioController == null)
        {
            return;
        }

        scenarioController.AvailableScenariosChanged -= RefreshScenarioList;
        scenarioController.StateChanged -= DisplayState;
        scenarioController.ScenarioCompleted -= DisplayState;
        scenarioController.ErrorOccurred -= DisplayError;
    }
}
