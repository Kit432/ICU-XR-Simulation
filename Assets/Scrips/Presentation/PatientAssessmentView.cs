using ICUSimulation.Scenarios;
using TMPro;
using UnityEngine;

public sealed class PatientAssessmentView : MonoBehaviour
{
    private ScenarioController scenarioController;
    private GameObject panel;
    private TMP_Text assessmentText;
    private TMP_Text respiratoryText;
    private TMP_Text heartRateText;
    private TMP_Text oxygenSaturationText;
    private bool subscribed;

    public void Initialize(ScenarioController controller, GameObject bedPanel)
    {
        Detach();
        scenarioController = controller;
        panel = bedPanel;
        ResolveTextReferences();
        Attach();
        Refresh(scenarioController != null ? scenarioController.CurrentState : null);
    }

    private void OnEnable()
    {
        Attach();
        if (scenarioController != null)
        {
            Refresh(scenarioController.CurrentState);
        }
    }

    private void OnDisable()
    {
        Detach();
    }

    private void Attach()
    {
        if (subscribed || scenarioController == null)
        {
            return;
        }

        scenarioController.StateChanged += HandleStateChanged;
        scenarioController.VitalsChanged += HandleVitalsChanged;
        scenarioController.FlagChanged += HandleFlagChanged;
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
        scenarioController.FlagChanged -= HandleFlagChanged;
        subscribed = false;
    }

    private void HandleStateChanged(ScenarioState state)
    {
        Refresh(state);
    }

    private void HandleVitalsChanged(ScenarioVitalsChangedEvent change)
    {
        Refresh(scenarioController.CurrentState);
    }

    private void HandleFlagChanged(ScenarioFlagChangedEvent change)
    {
        Refresh(scenarioController.CurrentState);
    }

    private void Refresh(ScenarioState state)
    {
        ScenarioPresentationSnapshot snapshot = ScenarioPresentationSnapshot.FromState(state);
        SetText(assessmentText, $"Assessment checklist: {snapshot.AssessmentStatusText}");
        SetText(respiratoryText, snapshot.HasScenario
            ? $"Respiratory rate: {snapshot.RespiratoryRate} /min"
            : "Respiratory rate: -- /min");
        SetText(heartRateText, snapshot.HeartRateText);
        SetText(oxygenSaturationText, snapshot.OxygenSaturationText);
    }

    private void ResolveTextReferences()
    {
        if (panel == null)
        {
            return;
        }

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            switch (text.gameObject.name)
            {
                case "Airway": assessmentText = text; break;
                case "Breathing": respiratoryText = text; break;
                case "Consciousness": heartRateText = text; break;
                case "SpO₂": oxygenSaturationText = text; break;
            }
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
