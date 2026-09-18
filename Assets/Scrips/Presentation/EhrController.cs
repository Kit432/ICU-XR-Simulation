using System;
using System.Collections.Generic;
using System.Linq;
using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class EhrController : MonoBehaviour
{
    private const int TrendCapacity = 6;

    [SerializeField] private ScenarioController scenarioController;

    private readonly DocumentationStore documentationStore = new DocumentationStore();
    private readonly List<int> heartRateTrend = new List<int>();
    private readonly List<int> oxygenSaturationTrend = new List<int>();
    private EhrView view;
    private bool subscribed;

    public event Action EhrOpened;
    public event Action EhrClosed;
    public event Action<EhrFormSubmittedEvent> EhrFormSubmitted;
    public event Action<DocumentationRequirementSatisfiedEvent> DocumentationRequirementSatisfied;
    public event Action ConfigurationChanged;
    public event Action DocumentationChanged;
    public event Action RequirementsChanged;

    public DocumentationStore DocumentationStore => documentationStore;
    public ScenarioController ScenarioController => scenarioController;
    public ScenarioDefinition ActiveDefinition => scenarioController != null ? scenarioController.ActiveDefinition : null;

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

        if (scenarioController == null)
        {
            Debug.LogError("EhrController requires a ScenarioController.", this);
            enabled = false;
            return;
        }

        scenarioController.GateRequirementResolver = documentationStore.HasFieldValue;
        Attach();
    }

    private void Start()
    {
        view = EhrView.Create(transform, this);
        Hotspot ehrHotspot = FindObjectsByType<Hotspot>(FindObjectsInactive.Include)
            .FirstOrDefault(hotspot => string.Equals(hotspot.HotspotId, "hs_ehr", StringComparison.Ordinal));
        if (ehrHotspot == null)
        {
            Debug.LogError("EhrController could not find hotspot 'hs_ehr'.", this);
            return;
        }

        ehrHotspot.interactionPanel = view.gameObject;
    }

    private void OnEnable()
    {
        Attach();
    }

    private void OnDisable()
    {
        Detach();
        CloseIfOpen();
    }

    public EhrValidationResult SubmitForm(string formId, IReadOnlyDictionary<string, string> values)
    {
        if (scenarioController.CurrentRunner?.IsRunning != true)
        {
            const string message = "Documentation is read-only outside an active training run.";
            scenarioController.RequestFeedback(message, ScenarioFeedbackStyle.Warning, formId);
            return new EhrValidationResult(Array.Empty<string>(), message);
        }

        if (!TryGetForm(formId, out EhrFormDefinition form))
        {
            scenarioController.RequestFeedback(
                "This EHR form is not available in the loaded scenario.",
                ScenarioFeedbackStyle.Warning,
                formId);
            return new EhrValidationResult(new[] { "unknown_form" });
        }

        Dictionary<string, string> controlledValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string fieldId in form.Fields ?? new List<string>())
        {
            controlledValues[fieldId] = values != null && values.TryGetValue(fieldId, out string value)
                ? value
                : string.Empty;
        }

        IReadOnlyList<string> requiredFields = GetRequiredFieldIds(formId);
        EhrValidationResult validation = EhrDocumentationValidator.Validate(requiredFields, controlledValues);
        if (!validation.IsValid)
        {
            string firstMissing = EhrDisplayNames.GetFieldLabel(validation.MissingFieldIds[0]);
            scenarioController.RequestFeedback(
                $"{firstMissing} is required.",
                ScenarioFeedbackStyle.Warning,
                formId);
            return validation;
        }

        documentationStore.SaveForm(formId, controlledValues);
        scenarioController.RecordDocumentation(formId, documentationStore.GetFormValues(formId));
        float runtimeTime = scenarioController.CurrentState?.ElapsedTime ?? 0f;
        EhrFormSubmittedEvent submitted = new EhrFormSubmittedEvent(
            formId,
            documentationStore.GetFormValues(formId),
            runtimeTime);
        EhrFormSubmitted?.Invoke(submitted);
        DocumentationChanged?.Invoke();
        scenarioController.RequestFeedback(
            $"{form.Title ?? EhrDisplayNames.GetFieldLabel(formId)} saved.",
            ScenarioFeedbackStyle.Success,
            formId);

        ReevaluateActiveGate();
        return validation;
    }

    public IReadOnlyList<string> GetRequiredFieldIds(string formId)
    {
        ScenarioNode node = scenarioController?.CurrentRunner?.CurrentNode;
        if (!IsGate(node) || node.GateRequirements?.RequiredForms == null)
        {
            return Array.Empty<string>();
        }

        RequiredForm requiredForm = node.GateRequirements.RequiredForms.FirstOrDefault(
            item => item != null && string.Equals(item.FormId, formId, StringComparison.Ordinal));
        if (requiredForm?.Fields != null)
        {
            return new List<string>(requiredForm.Fields);
        }

        return Array.Empty<string>();
    }

    public IReadOnlyList<string> GetMissingRequirementLines()
    {
        List<string> missing = new List<string>();
        ScenarioNode node = scenarioController?.CurrentRunner?.CurrentNode;
        if (!IsGate(node) || node.GateRequirements?.RequiredForms == null)
        {
            return missing;
        }

        foreach (RequiredForm form in node.GateRequirements.RequiredForms)
        {
            if (form == null)
            {
                continue;
            }

            foreach (string fieldId in form.Fields ?? new List<string>())
            {
                if (!documentationStore.HasFieldValue(form.FormId, fieldId))
                {
                    missing.Add(
                        $"{EhrDisplayNames.GetFormTitle(ActiveDefinition, form.FormId)} → " +
                        EhrDisplayNames.GetFieldLabel(fieldId));
                }
            }
        }

        return missing;
    }

    public string GetHeartRateTrend()
    {
        return FormatTrend(heartRateTrend);
    }

    public string GetOxygenSaturationTrend()
    {
        return FormatTrend(oxygenSaturationTrend);
    }

    public void NotifyOpened()
    {
        scenarioController.RecordInteraction("hs_ehr", "opened");
        EhrOpened?.Invoke();
    }

    public void NotifyClosed()
    {
        scenarioController.RecordInteraction("hs_ehr", "closed");
        EhrClosed?.Invoke();
    }

    public void CloseEhr()
    {
        if (view == null || !view.gameObject.activeSelf)
        {
            return;
        }

        if (UIFlowController.Instance != null)
        {
            UIFlowController.Instance.CloseActivePanel();
        }
        else
        {
            view.gameObject.SetActive(false);
        }
    }

    private void ReevaluateActiveGate()
    {
        ScenarioRunner runner = scenarioController.CurrentRunner;
        if (runner == null || !runner.IsWaitingAtGate)
        {
            RequirementsChanged?.Invoke();
            return;
        }

        string gateNodeId = runner.CurrentNode.Id;
        GateEvaluationResult evaluation = runner.EvaluateCurrentGate();
        RequirementsChanged?.Invoke();

        if (!evaluation.IsSatisfied)
        {
            return;
        }

        scenarioController.RequestFeedback(
            "Required documentation complete. Scenario continuing.",
            ScenarioFeedbackStyle.Success,
            gateNodeId);
    }

    private void HandleStateChanged(ScenarioState state)
    {
        if (scenarioController.CurrentRunner == null)
        {
            documentationStore.ResetForScenario(state?.ActiveScenarioId);
            ResetTrends(state);
            CloseIfOpen();
            ReconfigureView();
            DocumentationChanged?.Invoke();
            RequirementsChanged?.Invoke();
            return;
        }

        RecordVitals(state);
    }

    private void HandleVitalsChanged(ScenarioVitalsChangedEvent change)
    {
        RecordVitals(scenarioController.CurrentState);
    }

    private void HandleNodeEntered(ScenarioNode node)
    {
        RequirementsChanged?.Invoke();
    }

    private void HandleGateChanged(ScenarioGateEvent gate)
    {
        RequirementsChanged?.Invoke();
    }

    private void HandleGatePassed(ScenarioGateEvent gate)
    {
        RequirementsChanged?.Invoke();
        if (gate == null ||
            gate.WasDebugBypassed ||
            gate.Node?.GateRequirements?.RequiredForms == null ||
            gate.Node.GateRequirements.RequiredForms.Count == 0)
        {
            return;
        }

        DocumentationRequirementSatisfied?.Invoke(
            new DocumentationRequirementSatisfiedEvent(
                gate.Node.Id,
                scenarioController.CurrentState?.ElapsedTime ?? 0f));
    }

    private void Attach()
    {
        if (subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged += HandleStateChanged;
        scenarioController.VitalsChanged += HandleVitalsChanged;
        scenarioController.NodeEntered += HandleNodeEntered;
        scenarioController.GateBlocked += HandleGateChanged;
        scenarioController.GatePassed += HandleGatePassed;
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
        scenarioController.NodeEntered -= HandleNodeEntered;
        scenarioController.GateBlocked -= HandleGateChanged;
        scenarioController.GatePassed -= HandleGatePassed;
        subscribed = false;
    }

    private void ReconfigureView()
    {
        ConfigurationChanged?.Invoke();
        view?.RebuildFromDefinition();
    }

    private bool TryGetForm(string formId, out EhrFormDefinition form)
    {
        form = null;
        return ActiveDefinition?.EhrConfiguration?.Forms != null &&
               !string.IsNullOrWhiteSpace(formId) &&
               ActiveDefinition.EhrConfiguration.Forms.TryGetValue(formId, out form) &&
               form != null;
    }

    private void ResetTrends(ScenarioState state)
    {
        heartRateTrend.Clear();
        oxygenSaturationTrend.Clear();
        RecordVitals(state);
    }

    private void RecordVitals(ScenarioState state)
    {
        if (state == null)
        {
            return;
        }

        AddTrendValue(heartRateTrend, state.HeartRate);
        AddTrendValue(oxygenSaturationTrend, state.OxygenSaturation);
    }

    private static void AddTrendValue(List<int> trend, int value)
    {
        if (trend.Count > 0 && trend[trend.Count - 1] == value)
        {
            return;
        }

        trend.Add(value);
        if (trend.Count > TrendCapacity)
        {
            trend.RemoveAt(0);
        }
    }

    private static string FormatTrend(IReadOnlyList<int> trend)
    {
        return trend.Count == 0 ? "--" : string.Join(" → ", trend);
    }

    private void CloseIfOpen()
    {
        if (view != null && view.gameObject.activeSelf)
        {
            CloseEhr();
        }
    }

    private static bool IsGate(ScenarioNode node)
    {
        return node != null && string.Equals(node.Type, "gate", StringComparison.OrdinalIgnoreCase);
    }
}
