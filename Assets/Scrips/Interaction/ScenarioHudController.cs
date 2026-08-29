using ICUSimulation.Scenarios;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ScenarioHudController : MonoBehaviour
{
    [SerializeField] private ScenarioController scenarioController;

    private GateEvaluationResult currentGateEvaluation;
    private GUIStyle titleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle statusStyle;

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
            scenarioController.NodeEntered += HandleNodeEntered;
            scenarioController.GateBlocked += HandleGateBlocked;
            scenarioController.GatePassed += HandleGatePassed;
        }
    }

    private void Update()
    {
        if (scenarioController == null || Keyboard.current == null)
        {
            return;
        }

        if (UIFlowController.Instance != null && UIFlowController.Instance.HasOpenModal)
        {
            return;
        }

        if (Keyboard.current.f4Key.wasPressedThisFrame && scenarioController.ActiveDefinition != null)
        {
            scenarioController.StartScenario();
            return;
        }

        ScenarioRunner runner = scenarioController.CurrentRunner;
        if (runner == null || !runner.IsRunning)
        {
            return;
        }

        if ((Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame) &&
            IsNodeType(runner.CurrentNode, "message"))
        {
            scenarioController.ContinueCurrentMessage();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current.f8Key.wasPressedThisFrame && runner.IsWaitingAtGate)
        {
            scenarioController.DebugBypassCurrentGate();
        }
#endif
    }

    private void OnGUI()
    {
        if (scenarioController == null || scenarioController.ActiveDefinition == null)
        {
            return;
        }

        if (UIFlowController.Instance != null && UIFlowController.Instance.HasOpenModal)
        {
            return;
        }

        EnsureStyles();

        const float width = 520f;
        float height = Mathf.Min(650f, Screen.height - 32f);
        Rect outer = new Rect(16f, 16f, width, height);
        GUI.Box(outer, GUIContent.none);

        GUILayout.BeginArea(new Rect(outer.x + 16f, outer.y + 12f, outer.width - 32f, outer.height - 24f));
        GUILayout.Label(scenarioController.ActiveDefinition.Metadata.Title, titleStyle);

        ScenarioState state = scenarioController.CurrentState;
        ScenarioRunner runner = scenarioController.CurrentRunner;
        GUILayout.Label(GetStatusText(runner), statusStyle);

        if (state != null)
        {
            GUILayout.Label(
                $"Score {state.CurrentScore}   HR {state.HeartRate}   SpO2 {state.OxygenSaturation}%   " +
                $"RR {state.RespiratoryRate}   BP {state.BloodPressure}   Temp {state.Temperature:0.0} °C",
                bodyStyle);
        }

        GUILayout.Space(8f);

        if (runner == null)
        {
            GUILayout.Label("The scenario is loaded but has not started.", bodyStyle);
            if (GUILayout.Button("Start Scenario (F4)", GUILayout.Height(36f)))
            {
                scenarioController.StartScenario();
            }

            GUILayout.EndArea();
            return;
        }

        ScenarioNode node = runner.CurrentNode;
        if (node != null)
        {
            GUILayout.Label($"Node: {node.Id}", headingStyle);
            GUILayout.Label(node.Text ?? string.Empty, bodyStyle);
        }

        if (runner.IsCompleted)
        {
            GUILayout.Space(10f);
            GUILayout.Label("SCENARIO COMPLETE", titleStyle);
            GUILayout.Label($"Final score: {state?.CurrentScore ?? 0}", headingStyle);
            GUILayout.Label("Press F4 to start a fresh run. Full debrief is deferred to a later phase.", bodyStyle);
            GUILayout.EndArea();
            return;
        }

        if (runner.IsTimeoutActive)
        {
            GUILayout.Label($"Time remaining: {runner.TimeoutRemaining:0.0}s", statusStyle);
        }

        if (IsNodeType(node, "message"))
        {
            GUILayout.Space(10f);
            if (GUILayout.Button("Continue (Enter or C)", GUILayout.Height(36f)))
            {
                scenarioController.ContinueCurrentMessage();
            }
        }
        else if (IsNodeType(node, "decision"))
        {
            GUILayout.Space(8f);
            GUILayout.Label("Choose an action by interacting with the matching ICU hotspot:", headingStyle);
            foreach (ScenarioOption option in runner.CurrentOptions)
            {
                if (option != null)
                {
                    GUILayout.Label($"• {option.Label}", bodyStyle);
                }
            }
        }
        else if (IsNodeType(node, "gate"))
        {
            GUILayout.Space(8f);
            GUILayout.Label("Documentation gate blocked", statusStyle);
            DrawGateRequirements(node);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Space(8f);
            if (GUILayout.Button("DEBUG ONLY — Bypass Gate (F8)", GUILayout.Height(36f)))
            {
                scenarioController.DebugBypassCurrentGate();
            }
#endif
        }

        GUILayout.EndArea();
    }

    private void DrawGateRequirements(ScenarioNode node)
    {
        GateRequirements requirements = node.GateRequirements;
        if (!string.IsNullOrWhiteSpace(requirements?.TargetHotspot))
        {
            GUILayout.Label($"Required hotspot: {requirements.TargetHotspot}", bodyStyle);
        }

        if (currentGateEvaluation?.MissingRequirements == null)
        {
            return;
        }

        foreach (string requirement in currentGateEvaluation.MissingRequirements)
        {
            GUILayout.Label($"• Missing: {requirement}", bodyStyle);
        }
    }

    private void HandleNodeEntered(ScenarioNode node)
    {
        currentGateEvaluation = null;
    }

    private void HandleGateBlocked(ScenarioGateEvent gateEvent)
    {
        currentGateEvaluation = gateEvent?.Evaluation;
    }

    private void HandleGatePassed(ScenarioGateEvent gateEvent)
    {
        currentGateEvaluation = null;
    }

    private string GetStatusText(ScenarioRunner runner)
    {
        if (runner == null)
        {
            return "Status: Loaded — not started";
        }

        if (runner.IsCompleted)
        {
            return "Status: Complete";
        }

        return runner.IsRunning ? "Status: Running" : "Status: Stopped";
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true
        };
        statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.35f, 0.85f, 1f) },
            wordWrap = true
        };
    }

    private static bool IsNodeType(ScenarioNode node, string expectedType)
    {
        return node != null && string.Equals(node.Type, expectedType, System.StringComparison.OrdinalIgnoreCase);
    }

    private void OnDestroy()
    {
        if (scenarioController == null)
        {
            return;
        }

        scenarioController.NodeEntered -= HandleNodeEntered;
        scenarioController.GateBlocked -= HandleGateBlocked;
        scenarioController.GatePassed -= HandleGatePassed;
    }
}
