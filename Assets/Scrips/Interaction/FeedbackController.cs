using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.UI;

public sealed class FeedbackController : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;
    [SerializeField, Min(0.5f)] private float displaySeconds = 4f;
    public GameObject feedbackPanel;
    public Text feedbackText;
    public Image feedbackBackground;
    private ScenarioFeedbackRequest currentRequest;
    private float hideAt;

    private void Awake()
    {
        if (scenarioController == null) scenarioController = FindAnyObjectByType<ScenarioController>();
        if (scenarioController != null)
        {
            scenarioController.FeedbackRequested += ShowFeedback;
            scenarioController.StateChanged += HandleStateChanged;
            scenarioController.GlobalRuleDeactivated += HandleRuleDeactivated;
        }
    }
    private void Update()
    {
        if (currentRequest != null && Time.unscaledTime >= hideAt) ClearFeedback();
    }
    private void ShowFeedback(ScenarioFeedbackRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message)) return;
        // Feedback describes the most recent action. Historical messages remain in the session log,
        // rather than replaying obsolete alarms after the learner has already corrected the state.
        currentRequest = request;
        if (feedbackPanel == null) return;
        feedbackText.text = request.Style.ToString().ToUpperInvariant() + "  |  " + request.Message;
        feedbackBackground.color = GetStyleColor(request.Style);
        feedbackPanel.SetActive(true);
        feedbackPanel.transform.SetAsLastSibling();
        hideAt = Time.unscaledTime + displaySeconds;
    }
    private void ClearFeedback()
    {
        currentRequest = null;
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }
    private void HandleStateChanged(ScenarioState state)
    {
        if (scenarioController.CurrentRunner == null) ClearFeedback();
    }
    private void HandleRuleDeactivated(ScenarioGlobalRuleEvent rule)
    {
        if (currentRequest != null && currentRequest.SourceId == rule?.Rule?.Id) ClearFeedback();
    }
    private static Color GetStyleColor(ScenarioFeedbackStyle style)
    {
        switch (style)
        {
            case ScenarioFeedbackStyle.Success: return new Color(0.04f, 0.30f, 0.20f, 1f);
            case ScenarioFeedbackStyle.Warning: return new Color(0.40f, 0.25f, 0.02f, 1f);
            case ScenarioFeedbackStyle.Danger: return new Color(0.46f, 0.09f, 0.10f, 1f);
            default: return new Color(0.06f, 0.22f, 0.36f, 1f);
        }
    }
    private void OnDestroy()
    {
        if (scenarioController == null) return;
        scenarioController.FeedbackRequested -= ShowFeedback;
        scenarioController.StateChanged -= HandleStateChanged;
        scenarioController.GlobalRuleDeactivated -= HandleRuleDeactivated;
    }
}
