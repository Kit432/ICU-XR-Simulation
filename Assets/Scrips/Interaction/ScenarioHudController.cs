using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Updates the authored compact training HUD; no visual objects are generated at runtime.</summary>
public sealed class ScenarioHudController : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;
    public GameObject hudPanel;
    public Text titleText;
    public Text vitalsText;
    public Text statusText;
    public Text instructionText;
    public Text timeoutText;
    public Button continueButton;
    private GateEvaluationResult currentGateEvaluation;

    private void Awake()
    {
        if (scenarioController == null) scenarioController = FindAnyObjectByType<ScenarioController>();
        if (scenarioController != null)
        {
            scenarioController.NodeEntered += HandleNodeEntered;
            scenarioController.GateBlocked += HandleGateBlocked;
            scenarioController.GatePassed += HandleGatePassed;
        }
        if (continueButton != null) continueButton.onClick.AddListener(ContinueMessage);
    }

    private void Update()
    {
        bool visible = scenarioController != null && scenarioController.ActiveDefinition != null &&
            (UIFlowController.Instance == null || !UIFlowController.Instance.HasOpenModal);
        if (hudPanel != null) hudPanel.SetActive(visible);
        if (!visible || titleText == null) return;
        ScenarioRunner runner = scenarioController.CurrentRunner;
        ScenarioState state = scenarioController.CurrentState;
        titleText.text = scenarioController.ActiveDefinition.Metadata.Title;
        vitalsText.text = state == null ? string.Empty : $"HR {state.HeartRate}   SpO2 {state.OxygenSaturation}%   RR {state.RespiratoryRate}\nBP {state.BloodPressure}   Temp {state.Temperature:0.0} °C   Score {state.CurrentScore}";
        bool message = IsNodeType(runner?.CurrentNode, "message");
        continueButton.gameObject.SetActive(message);
        timeoutText.text = runner?.IsTimeoutActive == true ? $"DECISION TIME  {runner.TimeoutRemaining:0.0}s" : string.Empty;
        if (runner == null)
        {
            statusText.text = "READY TO START";
            instructionText.text = "Open Scenarios (F2) and select Start training.";
            return;
        }
        statusText.text = runner.IsCompleted ? "TRAINING COMPLETE" : runner.IsWaitingAtGate ? "DOCUMENTATION REQUIRED" : "TRAINING IN PROGRESS";
        string instructions = runner.CurrentNode?.Text ?? string.Empty;
        if (IsNodeType(runner.CurrentNode, "decision"))
        {
            instructions += "\n\nAim at the matching equipment and press E:\n";
            foreach (ScenarioOption option in runner.CurrentOptions)
                if (option != null) instructions += "\n• " + option.Label;
        }
        else if (runner.IsWaitingAtGate)
        {
            instructions += "\n\nUse the EHR Terminal. Save each required form.";
            if (currentGateEvaluation != null)
                foreach (string requirement in currentGateEvaluation.MissingRequirements)
                    instructions += "\n• " + FormatRequirement(requirement);
        }
        else if (runner.IsCompleted) instructions += "\n\nOpen Scenarios (F2) → Review session to see your debrief.";
        instructionText.text = instructions;
        if (message && Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame)) ContinueMessage();
    }

    public void ContinueMessage() => scenarioController.ContinueCurrentMessage();
    private string FormatRequirement(string requirement)
    {
        int split = requirement.IndexOf('.');
        return split < 1 ? EhrDisplayNames.GetFieldLabel(requirement) :
            EhrDisplayNames.GetFormTitle(scenarioController.ActiveDefinition, requirement.Substring(0, split)) + ": " + EhrDisplayNames.GetFieldLabel(requirement.Substring(split + 1));
    }
    private void HandleNodeEntered(ScenarioNode node) => currentGateEvaluation = null;
    private void HandleGateBlocked(ScenarioGateEvent e) => currentGateEvaluation = e?.Evaluation;
    private void HandleGatePassed(ScenarioGateEvent e) => currentGateEvaluation = null;
    private static bool IsNodeType(ScenarioNode node, string type) => node != null && string.Equals(node.Type, type, System.StringComparison.OrdinalIgnoreCase);
    private void OnDestroy()
    {
        if (scenarioController == null) return;
        scenarioController.NodeEntered -= HandleNodeEntered;
        scenarioController.GateBlocked -= HandleGateBlocked;
        scenarioController.GatePassed -= HandleGatePassed;
    }
}
