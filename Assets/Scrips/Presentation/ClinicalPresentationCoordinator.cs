using System;
using System.Collections.Generic;
using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClinicalPresentationCoordinator : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;

    private Font runtimeFont;
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

        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            panel = CreateVentilatorPanel(out settingText, out oxygenText, out interventionText);
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

    private GameObject CreateVentilatorPanel(
        out Text settingText,
        out Text oxygenText,
        out Text interventionText)
    {
        GameObject panel = new GameObject(
            "VentilatorPanel_Runtime",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(650f, 440f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.075f, 0.10f, 0.98f);

        Text title = CreateText(panel.transform, "Title", "VENTILATOR", 30, FontStyle.Bold, new Vector2(0f, 145f));
        title.color = new Color(0.30f, 0.90f, 1f);
        settingText = CreateText(panel.transform, "SettingText", string.Empty, 22, FontStyle.Normal, new Vector2(0f, 70f));
        oxygenText = CreateText(panel.transform, "VentilatorSpO2Text", string.Empty, 28, FontStyle.Bold, new Vector2(0f, 5f));
        interventionText = CreateText(panel.transform, "InterventionText", string.Empty, 23, FontStyle.Bold, new Vector2(0f, -60f));
        Text note = CreateText(
            panel.transform,
            "ClinicalNote",
            "Presentation reflects scenario state; this is not a ventilator control model.",
            17,
            FontStyle.Italic,
            new Vector2(0f, -125f));
        note.color = new Color(0.75f, 0.82f, 0.86f);
        Text close = CreateText(panel.transform, "CloseMessage", "Press Esc to close", 16, FontStyle.Normal, new Vector2(0f, -180f));
        close.color = new Color(0.65f, 0.72f, 0.76f);
        panel.SetActive(false);
        return panel;
    }

    private Text CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Vector2 position)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(590f, 52f);
        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = content;
        text.raycastTarget = false;
        return text;
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
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
