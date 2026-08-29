using ICUSimulation.Scenarios;
using UnityEngine;

public sealed class ScenarioHotspotCoordinator : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;

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
    }

    private void OnEnable()
    {
        Hotspot.HotspotInteracted += HandleHotspotInteracted;
    }

    private void OnDisable()
    {
        Hotspot.HotspotInteracted -= HandleHotspotInteracted;
    }

    private void HandleHotspotInteracted(Hotspot hotspot)
    {
        if (scenarioController == null || hotspot == null)
        {
            return;
        }

        scenarioController.HandleHotspotInteraction(hotspot.HotspotId);
    }
}
