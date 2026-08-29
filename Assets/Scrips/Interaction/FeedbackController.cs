using System.Collections.Generic;
using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class FeedbackController : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;
    [SerializeField, Min(0.5f)] private float displaySeconds = 4f;

    private readonly Queue<ScenarioFeedbackRequest> pendingRequests = new Queue<ScenarioFeedbackRequest>();
    private ScenarioFeedbackRequest currentRequest;
    private float hideAt;
    private GUIStyle feedbackStyle;

    private void Awake()
    {
        if (scenarioController == null)
        {
            scenarioController = GetComponent<ScenarioController>();
        }

        if (scenarioController == null)
        {
            scenarioController = FindAnyObjectByType<ScenarioController>();
        }

        if (scenarioController != null)
        {
            scenarioController.FeedbackRequested += Enqueue;
            scenarioController.StateChanged += HandleStateChanged;
        }
    }

    private void Update()
    {
        if (currentRequest != null && Time.unscaledTime >= hideAt)
        {
            currentRequest = null;
            ShowNext();
        }
    }

    private void Enqueue(ScenarioFeedbackRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return;
        }

        pendingRequests.Enqueue(request);
        if (currentRequest == null)
        {
            ShowNext();
        }
    }

    private void ShowNext()
    {
        if (pendingRequests.Count == 0)
        {
            currentRequest = null;
            return;
        }

        currentRequest = pendingRequests.Dequeue();
        hideAt = Time.unscaledTime + displaySeconds;
    }

    private void HandleStateChanged(ScenarioState state)
    {
        if (scenarioController != null && scenarioController.CurrentRunner == null)
        {
            pendingRequests.Clear();
            currentRequest = null;
        }
    }

    private void OnGUI()
    {
        if (currentRequest == null)
        {
            return;
        }

        if (feedbackStyle == null)
        {
            feedbackStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = true,
                padding = new RectOffset(18, 18, 12, 12)
            };
        }

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = GetStyleColor(currentRequest.Style);
        GUI.Box(
            new Rect((Screen.width - 620f) * 0.5f, 22f, 620f, 68f),
            currentRequest.Message,
            feedbackStyle);
        GUI.backgroundColor = previousColor;
    }

    private static Color GetStyleColor(ScenarioFeedbackStyle style)
    {
        switch (style)
        {
            case ScenarioFeedbackStyle.Success: return new Color(0.2f, 0.65f, 0.3f, 0.95f);
            case ScenarioFeedbackStyle.Warning: return new Color(0.9f, 0.62f, 0.12f, 0.95f);
            case ScenarioFeedbackStyle.Danger: return new Color(0.85f, 0.2f, 0.18f, 0.95f);
            default: return new Color(0.15f, 0.45f, 0.75f, 0.95f);
        }
    }

    private void OnDestroy()
    {
        if (scenarioController == null)
        {
            return;
        }

        scenarioController.FeedbackRequested -= Enqueue;
        scenarioController.StateChanged -= HandleStateChanged;
    }
}
