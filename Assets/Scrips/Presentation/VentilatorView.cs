using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.UI;

public sealed class VentilatorView : MonoBehaviour
{
    private ScenarioController scenarioController;
    private Text settingText;
    private Text oxygenSaturationText;
    private Text interventionText;
    private bool subscribed;

    public void Initialize(
        ScenarioController controller,
        Text settingLabel,
        Text oxygenSaturationLabel,
        Text interventionLabel)
    {
        Detach();
        scenarioController = controller;
        settingText = settingLabel;
        oxygenSaturationText = oxygenSaturationLabel;
        interventionText = interventionLabel;
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
        if (settingText != null)
        {
            settingText.text = snapshot.OxygenAdjusted
                ? "FiO2 intervention: Scenario adjustment applied"
                : "FiO2 intervention: No adjustment applied";
        }

        if (oxygenSaturationText != null)
        {
            oxygenSaturationText.text = snapshot.OxygenSaturationText;
        }

        if (interventionText != null)
        {
            interventionText.text = $"Oxygen adjustment: {snapshot.OxygenAdjustmentText}";
            interventionText.color = snapshot.OxygenAdjusted
                ? new Color(0.2f, 0.9f, 0.45f)
                : new Color(1f, 0.75f, 0.25f);
        }
    }
}
