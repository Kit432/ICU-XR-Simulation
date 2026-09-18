using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICUSimulation.Scenarios;
using Newtonsoft.Json;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Opt-in acceptance runner for the real Windows player. Drives normal controls and
/// records evidence; no debug gates or test-only state transitions are used.
/// Launch with --acceptance-output followed by an absolute output directory.
/// </summary>
public sealed class ReleaseAcceptance : MonoBehaviour
{
    private string output;
    private ScenarioController scenario;
    private EhrController ehr;
    private EhrView ehrView;
    private readonly List<string> checks = new List<string>();
    private readonly List<string> errors = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--acceptance-output");
        if (index < 0 || index + 1 >= args.Length) return;
        var runner = new GameObject("Acceptance verification").AddComponent<ReleaseAcceptance>();
        runner.output = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(runner.output);
        Application.logMessageReceived += runner.Log;
        runner.StartCoroutine(runner.Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);
        var sequences = new Stack<IEnumerator>();
        sequences.Push(Verify());
        while (sequences.Count > 0)
        {
            object current = null;
            bool more;
            try { more = sequences.Peek().MoveNext(); if (more) current = sequences.Peek().Current; }
            catch (Exception e) { errors.Add(e.ToString()); break; }
            if (!more) { sequences.Pop(); continue; }
            if (current is IEnumerator nested) { sequences.Push(nested); continue; }
            yield return current;
        }
        File.WriteAllText(Path.Combine(output, "acceptance-results.json"), JsonConvert.SerializeObject(new
        {
            completedUtc = DateTime.UtcNow.ToString("O"),
            unity = Application.unityVersion,
            platform = Application.platform.ToString(),
            width = Screen.width, height = Screen.height,
            method = "Automated standalone player; normal controllers, hotspot events, EHR input fields and save button callbacks; no gate bypass",
            passed = errors.Count == 0, checks, errors
        }, Formatting.Indented));
        Application.logMessageReceived -= Log;
        Application.Quit(errors.Count == 0 ? 0 : 1);
    }

    private IEnumerator Verify()
    {
        scenario = FindAnyObjectByType<ScenarioController>();
        ehr = FindAnyObjectByType<EhrController>();
        ehrView = FindAnyObjectByType<EhrView>(FindObjectsInactive.Include);
        Check(scenario != null && ehr != null && ehrView != null, "Scene includes scenario, EHR and authored UI components");
        Check(FindObjectsByType<Hotspot>(FindObjectsInactive.Include).Select(h => h.HotspotId).Distinct().Count() >= 5, "Five distinct equipment hotspots exist");
        Check(scenario.AvailableScenarios.Count >= 2, "Two JSON scenarios discovered from files");
        int reference = scenario.AvailableScenarios.ToList().FindIndex(s => s.Id == "icu_scenario_hypoxia_v1");
        var release = FindAnyObjectByType<ReleaseUiController>();
        release.scenarioDropdown.value = reference;
        yield return ClickThroughInput(release.loadButton);
        Check(scenario.ActiveDefinition != null && scenario.CurrentState != null,
            "Reference scenario loaded through the menu pointer event and persistent button binding");
        yield return null;
        Check(scenario.CurrentRunner == null, "Load prepares state without starting the runtime");
        yield return Capture("01_scenario_menu");
        Check(!ehr.SubmitForm("assessment_form", new Dictionary<string, string> { ["observation"] = "Not yet started" }).IsValid,
            "Documentation service rejects writes before Start");
        yield return ClickThroughInput(release.startButton);
        Check(scenario.CurrentRunner?.IsRunning == true, "Start training button begins the scenario through pointer input");
        yield return null;
        yield return Capture("02_icu_overview");
        Interact("hs_monitor");
        yield return null;
        Check(!FindAnyObjectByType<FirstPersonController>().enabled, "Opening equipment panel locks movement");
        var playerInputs = FindAnyObjectByType<StarterAssetsInputs>();
        playerInputs.SendMessage("OnApplicationFocus", true);
        Check(Cursor.lockState == CursorLockMode.None && !playerInputs.cursorInputForLook,
            "Returning focus to an open panel keeps its pointer unlocked and look input disabled");
        Check(ehr.ScenarioController.CurrentState.OxygenSaturation == 88, "Shared initial SpO2 value is 88");
        yield return Capture("03_monitor_alarm");
        Close();
        Check(FindAnyObjectByType<FirstPersonController>().enabled, "Closing panel restores movement");
        Check(playerInputs.cursorLocked && playerInputs.cursorInputForLook,
            "Closing a panel restores the first-person cursor and look preferences");
        Check(scenario.ContinueCurrentMessage(), "Continue advances introduction");
        Interact("hs_patient");
        yield return Capture("03b_patient_assessment");
        Close();
        Interact("hs_ventilator");
        yield return Capture("03c_ventilator");
        Close();
        Check(scenario.CurrentRunner.CurrentNode.Id == "n4_gate_documentation_1", "Assessment and intervention reach first documentation gate");
        Check(scenario.CurrentState.OxygenSaturation == 94, "Intervention updates shared SpO2 to 94");
        Interact("hs_ehr");
        yield return null;
        SelectForm("assessment_form"); SaveForm("assessment_form");
        Check(ehr.DocumentationStore.SubmittedFormCount == 0, "Empty assessment is rejected by visible save control");
        Check(scenario.CurrentRunner.IsWaitingAtGate, "Empty forms cannot pass the gate");
        yield return Capture("04_ehr_validation");
        SetInput("assessment_form", "observation", "Cyanosis and increased respiratory rate observed in the simulation.");
        SaveForm("assessment_form");
        Check(scenario.CurrentRunner.IsWaitingAtGate, "Saving only observation leaves intervention requirement blocked");
        SelectForm("intervention_form");
        SetInput("intervention_form", "fiO2_setting", "100%");
        SaveForm("intervention_form");
        Check(scenario.CurrentRunner.CurrentNode.Id == "n5_reassessment", "Both required saved fields release the first gate normally");
        Check(ehr.GetOxygenSaturationTrend().Contains("88") && ehr.GetOxygenSaturationTrend().Contains("94"), "EHR trend includes initial and updated values");
        int score = scenario.CurrentState.CurrentScore;
        SaveForm("intervention_form");
        Check(scenario.CurrentState.CurrentScore == score, "Repeat submission does not award gate points twice");
        yield return Capture("05_ehr_saved");
        Close(); scenario.ContinueCurrentMessage();
        Interact("hs_call"); Close();
        Check(scenario.CurrentRunner.CurrentNode.Id == "n7_gate_documentation_2", "Call action reaches communication gate");
        Interact("hs_ehr"); yield return null;
        SelectForm("communication_log");
        SetInput("communication_log", "recipient", "On-call physician (simulated)");
        SaveForm("communication_log");
        Check(scenario.CurrentRunner.IsWaitingAtGate, "Recipient without outcome is rejected");
        SetInput("communication_log", "outcome", "Simulated physician notified; reassessment requested.");
        SaveForm("communication_log");
        yield return null;
        Check(scenario.CurrentRunner.IsCompleted, "Both gates completed with saved documentation and no bypass");
        Check(scenario.CurrentState.CurrentScore == 155, "Complete assessment, intervention and escalation route scores 155");
        Check(scenario.BuildDebrief().Completed, "Debrief reports actual completion");
        ShowReleasePanel("OpenDebrief");
        yield return Capture("06_debrief");
        Check(scenario.TryExportSession(out string path, out string error), "Session JSON exported: " + error);
        Check(File.Exists(path), "Exported file exists on disk");
        File.Copy(path, Path.Combine(output, "completed-session.json"), true);
        ShowReleasePanel("OpenHelp");
        yield return Capture("07_help");
        string savedOutcome = ehr.DocumentationStore.GetFieldValue("communication_log", "outcome");
        Check(!ehr.SubmitForm("communication_log", new Dictionary<string, string> { ["outcome"] = "Post-completion edit" }).IsValid &&
            ehr.DocumentationStore.GetFieldValue("communication_log", "outcome") == savedOutcome,
            "Completed documentation remains immutable even through a direct service call");
        Close();
        Check(scenario.ResetScenario(), "Reset succeeds");
        yield return null;
        Check(ehr.DocumentationStore.SubmittedFormCount == 0, "Reset clears saved documentation");
        Check(scenario.CurrentState.CurrentScore == 100 && scenario.CurrentState.OxygenSaturation == 88 && scenario.CurrentState.ElapsedTime == 0, "Reset restores score, vitals and clock");
        Check(scenario.CurrentState.Flags.Values.All(v => !v), "Reset clears all scenario flags");
        Check(scenario.CurrentRunner == null, "Reset stops active runtime and timers");
        int second = scenario.AvailableScenarios.ToList().FindIndex(s => s.Id == "icu_scenario_reassessment_v2");
        var releaseUi = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).First(x => x.GetType().Name == "ReleaseUiController");
        ((Dropdown)releaseUi.GetType().GetField("scenarioDropdown").GetValue(releaseUi)).value = second;
        Check(scenario.LoadScenario(second) && scenario.StartScenario(), "Second JSON starts without source changes");
        Close(); scenario.ContinueCurrentMessage();
        yield return null;
        ShowReleasePanel("OpenHelp");
        yield return null;
        float remaining = scenario.CurrentRunner.TimeoutRemaining;
        yield return new WaitForSecondsRealtime(.3f);
        Check(Math.Abs(scenario.CurrentRunner.TimeoutRemaining - remaining) < .05f, "Help pauses the decision timer");
        Close();
        yield return new WaitForSecondsRealtime(.2f);
        Check(scenario.CurrentRunner.TimeoutRemaining < remaining, "Closing Help resumes the decision timer");
        scenario.ResetScenario();
        Check(!new ScenarioLoader().LoadFromJson("{ invalid").IsSuccess, "Malformed JSON produces load failure");
        ShowReleasePanel("OpenMenu");
        yield return Capture("08_second_scenario");
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description);
    }
    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.3f);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        for (int i = 0; i < 12; i++) yield return null;
    }
    private static IEnumerator ClickThroughInput(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point });
        yield return null;
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point }.WithButton(MouseButton.Left));
        yield return null;
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point });
        yield return null;
    }
    private static void Close() => UIFlowController.Instance.CloseActivePanel();
    private static void Interact(string id) => FindObjectsByType<Hotspot>(FindObjectsInactive.Include).First(h => h.HotspotId == id).Interact();
    private void SelectForm(string id) => ehrView.transform.Find("FormTabs/Content/" + id + "Tab").GetComponent<Button>().onClick.Invoke();
    private void SetInput(string form, string field, string value) => ehrView.transform.Find("Forms/" + form + "Panel/Fields/Viewport/Content/" + field + "Row/Input").GetComponent<TMP_InputField>().text = value;
    private void SaveForm(string form) => ehrView.transform.Find("Forms/" + form + "Panel/SubmitButton").GetComponent<Button>().onClick.Invoke();
    private static void ShowReleasePanel(string method)
    {
        var controller = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).First(x => x.GetType().Name == "ReleaseUiController");
        controller.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(controller, null);
    }
}

