using System;
using System.Collections.Generic;
using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClinicalPresentationCoordinator : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;

    private bool configured;

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

    private void Start()
    {
        ConfigurePresentation();
    }

    public void ConfigurePresentation()
    {
        if (configured || scenarioController == null)
        {
            return;
        }

        Dictionary<string, Hotspot> hotspots = FindHotspots();
        GameObject monitorPanel = FindDescendant("MonitorPanel");
        GameObject bedPanel = FindDescendant("BedPanel");

        MonitorView monitorView = EnsureComponent<MonitorView>();
        monitorView.Initialize(scenarioController, monitorPanel);

        PatientAssessmentView patientView = EnsureComponent<PatientAssessmentView>();
        patientView.Initialize(scenarioController, bedPanel);

        ConfigureVentilator(hotspots);
        ConfigureCallButton(hotspots);

        AlarmAudioController alarmAudio = EnsureComponent<AlarmAudioController>();
        alarmAudio.Initialize(scenarioController);
        configured = true;
    }

    private void ConfigureVentilator(IReadOnlyDictionary<string, Hotspot> hotspots)
    {
        if (!hotspots.TryGetValue("hs_ventilator", out Hotspot ventilator))
        {
            Debug.LogWarning("Clinical presentation could not find hotspot 'hs_ventilator'.", this);
            return;
        }

        GameObject panel = FindDescendant("VentilatorPanel_Runtime");
        Text settingText;
        Text oxygenText;
        Text interventionText;

        if (panel == null)
        {
            Debug.LogError("Assign the authored VentilatorPanel prefab to the scene.", this);
            return;
        }
        else
        {
            settingText = FindLegacyText(panel, "SettingText");
            oxygenText = FindLegacyText(panel, "VentilatorSpO2Text");
            interventionText = FindLegacyText(panel, "InterventionText");
        }

        ventilator.interactionPanel = panel;
        VentilatorView view = EnsureComponent<VentilatorView>();
        view.Initialize(scenarioController, settingText, oxygenText, interventionText);
    }

    private void ConfigureCallButton(IReadOnlyDictionary<string, Hotspot> hotspots)
    {
        if (!hotspots.TryGetValue("hs_call", out Hotspot callButton))
        {
            Debug.LogWarning("Clinical presentation could not find hotspot 'hs_call'.", this);
            return;
        }

        CallButtonView view = EnsureComponent<CallButtonView>();
        view.Initialize(scenarioController, callButton.transform);
    }

    private Dictionary<string, Hotspot> FindHotspots()
    {
        Dictionary<string, Hotspot> result = new Dictionary<string, Hotspot>(StringComparer.Ordinal);
        Hotspot[] hotspots = FindObjectsByType<Hotspot>(FindObjectsInactive.Include);
        foreach (Hotspot hotspot in hotspots)
        {
            if (hotspot != null && !string.IsNullOrWhiteSpace(hotspot.HotspotId))
            {
                result[hotspot.HotspotId] = hotspot;
            }
        }

        return result;
    }

    private GameObject FindDescendant(string objectName)
    {
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(candidate.name, objectName, StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Text FindLegacyText(GameObject root, string objectName)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (string.Equals(text.gameObject.name, objectName, StringComparison.Ordinal))
            {
                return text;
            }
        }

        return null;
    }

    private T EnsureComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component;
    }
}
