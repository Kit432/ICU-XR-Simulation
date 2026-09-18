using System;
using System.Linq;
using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Behaviour for the authored TrainingInterface prefab. All layout lives in Unity assets.</summary>
public sealed class ReleaseUiController : MonoBehaviour
{
    public ScenarioController scenarioController;
    public GameObject menuPanel;
    public GameObject helpPanel;
    public GameObject debriefPanel;
    public GameObject resetPanel;
    public Dropdown scenarioDropdown;
    public Text scenarioDescription;
    public Text menuStatus;
    public Text errorText;
    public Text debriefText;
    public Text exportStatus;
    public Text helpContext;
    public Button loadButton;
    public Button startButton;
    public Button resetButton;
    public Button resumeButton;
    public Button exportButton;
    public Button reviewButton;
    private GameObject returnPanel;
    private string lastExportPath;

    private void Awake()
    {
        if (scenarioController == null) scenarioController = FindAnyObjectByType<ScenarioController>();
        if (scenarioController == null) { enabled = false; return; }
        scenarioController.AvailableScenariosChanged += RefreshScenarioList;
        scenarioController.StateChanged += RefreshState;
        scenarioController.ErrorOccurred += ShowError;
        scenarioController.ScenarioCompleted += HandleCompleted;
        scenarioDropdown.onValueChanged.AddListener(_ => RefreshSelection());
    }

    private void Start()
    {
        RefreshScenarioList();
        RefreshState(scenarioController.CurrentState);
        OpenMenu();
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame) OpenHelp();
            else if (Keyboard.current.f2Key.wasPressedThisFrame) OpenMenu();
        }
        // Clinical panels keep the scenario clock running. Help and the session menu pause it.
        scenarioController.IsPaused = menuPanel.activeSelf || helpPanel.activeSelf || resetPanel.activeSelf || debriefPanel.activeSelf;
    }

    public void OpenMenu()
    {
        RefreshState(scenarioController.CurrentState);
        UIFlowController.Instance.OpenPanel(menuPanel);
        scenarioController.IsPaused = true;
    }

    public void OpenHelp()
    {
        if (helpPanel.activeSelf) { CloseHelp(); return; }
        returnPanel = UIFlowController.Instance.ActivePanel;
        ScenarioRunner runner = scenarioController.CurrentRunner;
        helpContext.text = runner?.CurrentNode == null ? "GETTING STARTED\nLoad a scenario in Scenarios (F2), then choose Start training."
            : runner.IsWaitingAtGate ? "CURRENT STEP: DOCUMENTATION\nOpen the EHR Terminal. Complete each field marked * and choose Save / Submit on each required form."
            : "CURRENT STEP\n" + runner.CurrentNode.Text;
        UIFlowController.Instance.OpenPanel(helpPanel);
        scenarioController.IsPaused = true;
    }

    public void CloseHelp()
    {
        if (returnPanel != null && returnPanel != helpPanel)
        {
            UIFlowController.Instance.OpenPanel(returnPanel);
            returnPanel = null;
        }
        else ClosePanel();
    }

    public void ClosePanel()
    {
        UIFlowController.Instance.CloseActivePanel();
        scenarioController.IsPaused = false;
    }

    public void RefreshScenarios()
    {
        scenarioController.RefreshScenarios();
    }

    public void LoadSelectedScenario()
    {
        if (scenarioController.CurrentRunner?.IsRunning == true)
        {
            ShowError("A training run is active. Reset it before loading another scenario; export the current log first if needed.");
            return;
        }
        if (scenarioController.LoadScenario(scenarioDropdown.value))
        {
            lastExportPath = null;
            errorText.text = string.Empty;
            RefreshState(scenarioController.CurrentState);
        }
    }

    public void StartTraining()
    {
        if (scenarioController.ActiveDefinition == null)
        {
            ShowError("Choose a scenario and select Load selected first.");
            return;
        }
        if (scenarioController.CurrentRunner != null)
        {
            ShowError("Reset the current run before starting again.");
            return;
        }
        if (scenarioController.StartScenario())
        {
            lastExportPath = null;
            ClosePanel();
        }
    }

    public void AskReset()
    {
        UIFlowController.Instance.OpenPanel(resetPanel);
        scenarioController.IsPaused = true;
    }

    public void ConfirmReset()
    {
        scenarioController.ResetScenario();
        lastExportPath = null;
        exportStatus.text = string.Empty;
        errorText.text = string.Empty;
        OpenMenu();
    }

    public void OpenDebrief()
    {
        ScenarioDebriefReport report = scenarioController.BuildDebrief();
        debriefText.text = report == null ? "Start a training run to create a review." : report.SummaryText;
        exportStatus.text = string.IsNullOrEmpty(lastExportPath)
            ? "Export the complete event history, final state and documentation as JSON."
            : "Saved to: " + lastExportPath;
        exportButton.interactable = scenarioController.SessionLog != null;
        UIFlowController.Instance.OpenPanel(debriefPanel);
    }

    public void ExportLog()
    {
        if (scenarioController.TryExportSession(out string path, out string error))
        {
            lastExportPath = path;
            exportStatus.text = "JSON saved successfully:\n" + path;
        }
        else exportStatus.text = "Export failed: " + error;
    }

    public void CopyExportPath()
    {
        if (string.IsNullOrEmpty(lastExportPath)) { exportStatus.text = "Choose Export JSON first."; return; }
        GUIUtility.systemCopyBuffer = lastExportPath;
        exportStatus.text = "Path copied to clipboard:\n" + lastExportPath;
    }

    private void HandleCompleted(ScenarioState state) => OpenDebrief();

    private void RefreshScenarioList()
    {
        int selection = scenarioDropdown.value;
        scenarioDropdown.ClearOptions();
        var titles = scenarioController.AvailableScenarios.Select(s => s.Title ?? s.FileName).ToList();
        if (titles.Count == 0) titles.Add("No valid scenarios found");
        scenarioDropdown.AddOptions(titles);
        scenarioDropdown.value = Mathf.Clamp(selection, 0, titles.Count - 1);
        scenarioDropdown.interactable = scenarioController.AvailableScenarios.Count > 0;
        loadButton.interactable = scenarioController.AvailableScenarios.Count > 0;
        RefreshSelection();
        ShowError(scenarioController.LastError);
    }

    private void RefreshSelection()
    {
        var scenarios = scenarioController.AvailableScenarios;
        if (scenarios.Count == 0) { scenarioDescription.text = "Add valid JSON files to the Scenarios folder beside the application data, then choose Refresh list."; return; }
        var s = scenarios[Mathf.Clamp(scenarioDropdown.value, 0, scenarios.Count - 1)];
        scenarioDescription.text = s.Metadata?.Description ?? s.Title ?? s.FileName;
    }

    private void RefreshState(ScenarioState state)
    {
        bool loaded = scenarioController.ActiveDefinition != null;
        ScenarioRunner runner = scenarioController.CurrentRunner;
        bool active = runner?.IsRunning == true;
        string status = !loaded ? "Choose a scenario below to begin." : runner == null ? "Ready to start" : runner.IsCompleted ? "Training complete — review and export your session" : "Training paused while this menu is open";
        menuStatus.text = loaded ? scenarioController.ActiveDefinition.Metadata.Title + "\n" + status : status;
        startButton.interactable = loaded && runner == null;
        resetButton.interactable = loaded;
        resumeButton.interactable = loaded;
        reviewButton.interactable = runner != null;
        loadButton.interactable = !active && scenarioController.AvailableScenarios.Count > 0;
    }

    private void ShowError(string error) { if (errorText != null) errorText.text = error ?? string.Empty; }

    private void OnDestroy()
    {
        if (scenarioController == null) return;
        scenarioController.IsPaused = false;
        scenarioController.AvailableScenariosChanged -= RefreshScenarioList;
        scenarioController.StateChanged -= RefreshState;
        scenarioController.ErrorOccurred -= ShowError;
        scenarioController.ScenarioCompleted -= HandleCompleted;
    }
}

