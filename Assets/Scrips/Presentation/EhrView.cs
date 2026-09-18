using System;
using System.Collections.Generic;
using System.Linq;
using ICUSimulation.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EhrView : MonoBehaviour
{
    private sealed class FormUi
    {
        public string FormId;
        public string Title;
        public GameObject Panel;
        public TMP_Text TabLabel;
        public TMP_Text StatusLabel;
        public TMP_Text RequirementLabel;
        public TMP_Text ValidationLabel;
        public readonly Dictionary<string, TMP_InputField> Inputs =
            new Dictionary<string, TMP_InputField>(StringComparer.Ordinal);
        public readonly Dictionary<string, TMP_Text> FieldLabels =
            new Dictionary<string, TMP_Text>(StringComparer.Ordinal);
        public bool Dirty;
    }

    private readonly Dictionary<string, FormUi> forms =
        new Dictionary<string, FormUi>(StringComparer.Ordinal);

    private EhrController ehrController;
    private ScenarioController scenarioController;
    private RectTransform tabContainer;
    private RectTransform formContainer;
    private TMP_Text overviewText;
    private TMP_Text vitalsText;
    private TMP_Text requirementBanner;
    private TMP_Text emptyFormsText;
    private string activeFormId;
    private bool initialized;
    private bool subscribed;

    // The complete layout and row templates are editable in the EhrPanel prefab.
    private GameObject formTemplate;
    private GameObject tabTemplate;
    private GameObject fieldTemplate;

    public static EhrView Create(Transform parent, EhrController controller)
    {
        EhrView view = parent.GetComponentInChildren<EhrView>(true);
        if (view == null)
        {
            Debug.LogError("Assign the authored EhrPanel prefab to the scene.");
            return null;
        }
        view.gameObject.SetActive(false);
        view.Initialize(controller);
        return view;
    }

    public void RebuildFromDefinition()
    {
        if (!initialized)
        {
            return;
        }

        ClearGeneratedForms();
        ScenarioDefinition definition = ehrController.ActiveDefinition;
        if (definition?.EhrConfiguration?.Forms == null || definition.EhrConfiguration.Forms.Count == 0)
        {
            emptyFormsText.gameObject.SetActive(true);
            emptyFormsText.text = definition == null
                ? "Load a scenario to configure EHR documentation forms."
                : "This scenario does not define EHR documentation forms.";
            RefreshAll();
            return;
        }

        emptyFormsText.gameObject.SetActive(false);
        int index = 0;
        int formCount = definition.EhrConfiguration.Forms.Count;
        foreach (KeyValuePair<string, EhrFormDefinition> configuredForm in definition.EhrConfiguration.Forms)
        {
            CreateForm(configuredForm.Key, configuredForm.Value, index, formCount);
            index++;
        }

        SelectForm(forms.Keys.FirstOrDefault());
        RefreshAll();
    }

    private void Initialize(EhrController controller)
    {
        ehrController = controller;
        scenarioController = controller != null ? controller.ScenarioController : null;
        if (!initialized)
        {
            tabContainer = (RectTransform)transform.Find("FormTabs/Content");
            formContainer = (RectTransform)transform.Find("Forms");
            overviewText = transform.Find("OverviewText").GetComponent<TMP_Text>();
            vitalsText = transform.Find("VitalsText").GetComponent<TMP_Text>();
            requirementBanner = transform.Find("RequirementBanner/RequirementText").GetComponent<TMP_Text>();
            emptyFormsText = formContainer.Find("EmptyFormsText").GetComponent<TMP_Text>();
            formTemplate = transform.Find("Templates/FormTemplate").gameObject;
            tabTemplate = transform.Find("Templates/TabTemplate").gameObject;
            fieldTemplate = transform.Find("Templates/FieldTemplate").gameObject;
            transform.Find("CloseButton").GetComponent<Button>().onClick.AddListener(() => ehrController.CloseEhr());
            initialized = true;
        }

        RebuildFromDefinition();
    }

    private void CreateForm(string formId, EhrFormDefinition definition, int index, int formCount)
    {
        string title = string.IsNullOrWhiteSpace(definition?.Title)
            ? EhrDisplayNames.GetFieldLabel(formId) : definition.Title;
        GameObject tabObject = Instantiate(tabTemplate, tabContainer, false);
        tabObject.name = formId + "Tab";
        tabObject.SetActive(true);
        Button tab = tabObject.GetComponent<Button>();
        TMP_Text tabLabel = tab.GetComponentInChildren<TMP_Text>(true);
        tabLabel.text = title;
        GameObject panel = Instantiate(formTemplate, formContainer, false);
        panel.name = formId + "Panel";
        FormUi form = new FormUi
        {
            FormId = formId, Title = title, Panel = panel, TabLabel = tabLabel,
            StatusLabel = panel.transform.Find("Status").GetComponent<TMP_Text>(),
            RequirementLabel = panel.transform.Find("CurrentRequirement").GetComponent<TMP_Text>(),
            ValidationLabel = panel.transform.Find("Validation").GetComponent<TMP_Text>()
        };
        panel.transform.Find("FormTitle").GetComponent<TMP_Text>().text = title;
        Transform fieldContainer = panel.transform.Find("Fields/Viewport/Content");
        foreach (string fieldId in definition?.Fields ?? new List<string>())
        {
            GameObject row = Instantiate(fieldTemplate, fieldContainer, false);
            row.name = fieldId + "Row";
            row.SetActive(true);
            TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
            TMP_InputField input = row.transform.Find("Input").GetComponent<TMP_InputField>();
            label.text = EhrDisplayNames.GetFieldLabel(fieldId);
            input.SetTextWithoutNotify(string.Empty);
            input.lineType = IsMultilineField(fieldId)
                ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            ((TMP_Text)input.placeholder).text = "Enter " + label.text.ToLowerInvariant();
            input.onValueChanged.AddListener(value => MarkDirty(formId));
            form.FieldLabels[fieldId] = label;
            form.Inputs[fieldId] = input;
        }
        panel.transform.Find("SubmitButton").GetComponent<Button>().onClick.AddListener(() => SubmitForm(formId));
        tab.onClick.AddListener(() => SelectForm(formId));
        panel.SetActive(false);
        forms[formId] = form;
    }

    private void SubmitForm(string formId)
    {
        if (!forms.TryGetValue(formId, out FormUi form))
        {
            return;
        }

        Dictionary<string, string> values = form.Inputs.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.text,
            StringComparer.Ordinal);
        EhrValidationResult validation = ehrController.SubmitForm(formId, values);
        if (!validation.IsValid)
        {
            form.ValidationLabel.color = new Color(1f, 0.55f, 0.35f);
            form.ValidationLabel.text = validation.ErrorMessage ?? "Required: " + string.Join(
                ", ",
                validation.MissingFieldIds.Select(EhrDisplayNames.GetFieldLabel));
            return;
        }

        form.Dirty = false;
        form.ValidationLabel.color = new Color(0.25f, 0.95f, 0.5f);
        form.ValidationLabel.text = "Documentation saved successfully.";
        RefreshDocumentationStatus();
        RefreshRequirements();
    }

    private void SelectForm(string formId)
    {
        activeFormId = formId;
        foreach (FormUi form in forms.Values)
        {
            bool selected = string.Equals(form.FormId, formId, StringComparison.Ordinal);
            form.Panel.SetActive(selected);
            Button tab = form.TabLabel != null ? form.TabLabel.GetComponentInParent<Button>() : null;
            if (tab != null)
            {
                tab.GetComponent<Image>().color = selected
                    ? new Color(0.12f, 0.48f, 0.68f, 1f)
                    : new Color(0.10f, 0.18f, 0.24f, 1f);
            }
        }
    }

    private void MarkDirty(string formId)
    {
        if (forms.TryGetValue(formId, out FormUi form))
        {
            form.Dirty = true;
            RefreshFormStatus(form);
        }
    }

    private void RefreshAll()
    {
        RefreshOverviewAndVitals();
        RefreshDocumentationStatus();
        RefreshRequirements();
    }

    private void RefreshOverviewAndVitals()
    {
        ScenarioDefinition definition = ehrController.ActiveDefinition;
        ScenarioState state = scenarioController?.CurrentState;
        ScenarioRunner runner = scenarioController?.CurrentRunner;
        string scenarioTitle = definition?.Metadata?.Title ?? "No scenario loaded";
        string status = runner == null ? "Loaded / not started" : runner.IsCompleted ? "Complete" : "Running";
        overviewText.text =
            $"Patient: ICU Patient\nScenario: {scenarioTitle}\nStatus: {status}   Score: {state?.CurrentScore ?? 0}\n" +
            $"Current context: {state?.CurrentNodeId ?? "--"}";

        ScenarioPresentationSnapshot snapshot = ScenarioPresentationSnapshot.FromState(state);
        vitalsText.text =
            $"{snapshot.HeartRateText}   {snapshot.OxygenSaturationText}   {snapshot.RespiratoryRateText}\n" +
            $"{snapshot.BloodPressureText}   {snapshot.TemperatureText}\n" +
            $"HR trend: {ehrController.GetHeartRateTrend()}   SpO2 trend: {ehrController.GetOxygenSaturationTrend()}";
    }

    private void RefreshDocumentationStatus()
    {
        foreach (FormUi form in forms.Values)
        {
            RefreshFormStatus(form);
        }
    }

    private void RefreshFormStatus(FormUi form)
    {
        bool saved = ehrController.DocumentationStore.HasFormSubmission(form.FormId);
        string state = saved ? form.Dirty ? "Unsaved changes" : "Saved" : "Not submitted";
        form.StatusLabel.text = state;
        form.StatusLabel.color = saved && !form.Dirty
            ? new Color(0.25f, 0.95f, 0.5f)
            : new Color(1f, 0.76f, 0.25f);
        if (form.TabLabel != null)
        {
            form.TabLabel.text = saved && !form.Dirty ? form.Title + "  (Saved)" : form.Title;
        }
    }

    private void RefreshRequirements()
    {
        IReadOnlyList<string> missing = ehrController.GetMissingRequirementLines();
        ScenarioRunner runner = scenarioController?.CurrentRunner;
        ScenarioNode node = runner?.CurrentNode;
        bool canEdit = runner != null && runner.IsRunning;
        bool atGate = node != null && string.Equals(node.Type, "gate", StringComparison.OrdinalIgnoreCase);
        requirementBanner.text = !canEdit
            ? runner != null && runner.IsCompleted
                ? "Session complete. Saved documentation is read-only. Export the debrief before resetting."
                : "Start training to save documentation for this scenario."
            : !atGate
            ? "No documentation gate is active. You may document optional information at any time."
            : missing.Count == 0
                ? "CURRENT DOCUMENTATION REQUIREMENTS COMPLETE"
                : "DOCUMENTATION REQUIRED — Missing: " + string.Join("  •  ", missing);
        requirementBanner.color = atGate && missing.Count > 0
            ? new Color(1f, 0.68f, 0.25f)
            : new Color(0.35f, 0.9f, 0.65f);

        foreach (FormUi form in forms.Values)
        {
            form.Panel.transform.Find("SubmitButton").GetComponent<Button>().interactable = canEdit;
            foreach (TMP_InputField input in form.Inputs.Values) input.interactable = canEdit;
            HashSet<string> required = new HashSet<string>(
                ehrController.GetRequiredFieldIds(form.FormId),
                StringComparer.Ordinal);
            form.RequirementLabel.text = required.Count > 0 ? "REQUIRED FOR CURRENT STEP" : "OPTIONAL FOR CURRENT STEP";
            form.RequirementLabel.color = required.Count > 0
                ? new Color(1f, 0.7f, 0.25f)
                : new Color(0.65f, 0.75f, 0.82f);

            foreach (KeyValuePair<string, TMP_Text> field in form.FieldLabels)
            {
                field.Value.text = EhrDisplayNames.GetFieldLabel(field.Key) + (required.Contains(field.Key) ? " *" : string.Empty);
                field.Value.color = required.Contains(field.Key)
                    ? new Color(1f, 0.82f, 0.35f)
                    : Color.white;
            }
        }
    }

    private void ClearGeneratedForms()
    {
        foreach (Transform child in tabContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        foreach (Transform child in formContainer)
        {
            if (child != emptyFormsText.transform)
            {
                child.gameObject.SetActive(false);
            Destroy(child.gameObject);
            }
        }

        forms.Clear();
        activeFormId = null;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        Attach();
        RefreshAll();
        ehrController.NotifyOpened();
    }

    private void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        Detach();
        ehrController.NotifyClosed();
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
        ehrController.DocumentationChanged += RefreshDocumentationStatus;
        ehrController.RequirementsChanged += RefreshRequirements;
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
        ehrController.DocumentationChanged -= RefreshDocumentationStatus;
        ehrController.RequirementsChanged -= RefreshRequirements;
        subscribed = false;
    }

    private void HandleStateChanged(ScenarioState state)
    {
        RefreshOverviewAndVitals();
    }

    private void HandleVitalsChanged(ScenarioVitalsChangedEvent change)
    {
        RefreshOverviewAndVitals();
    }

    private void HandleNodeEntered(ScenarioNode node)
    {
        RefreshAll();
    }

    private static bool IsMultilineField(string fieldId)
    {
        return string.Equals(fieldId, "observation", StringComparison.Ordinal) ||
               string.Equals(fieldId, "reason", StringComparison.Ordinal) ||
               string.Equals(fieldId, "outcome", StringComparison.Ordinal);
    }

}
