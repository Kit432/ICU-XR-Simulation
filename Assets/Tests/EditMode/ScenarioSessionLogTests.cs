using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICUSimulation.Scenarios;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ICUSimulation.Tests.EditMode
{
    public sealed class ScenarioSessionLogTests
    {
        private ScenarioRunner runner;
        private DocumentationStore documentation;

        [SetUp]
        public void SetUp()
        {
            ScenarioLoadResult loaded = new ScenarioLoader().LoadFromFile(Path.Combine(Application.dataPath,
                "StreamingAssets", "Scenarios", "icu_scenario_hypoxia_v1.json"));
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            runner = new ScenarioRunner(loaded.Definition, ScenarioState.CreateFromDefinition(loaded.Definition));
            documentation = new DocumentationStore();
            runner.GateRequirementResolver = documentation.HasFieldValue;
            runner.Start();
        }

        [Test]
        public void EventSnapshots_PreserveVitalsFlagsAndElapsedTimeAtEvent()
        {
            ScenarioLogEntry initial = runner.SessionLog.Entries.First(e => e.EventType == "SCENARIO_STARTED");
            runner.ContinueMessage();
            runner.Tick(2.5f);
            runner.TryHandleHotspotInteraction("hs_patient");
            runner.TryHandleHotspotInteraction("hs_ventilator");

            Assert.That(initial.State.OxygenSaturation, Is.EqualTo(88));
            Assert.That(initial.State.Flags["assessment_complete"], Is.False);
            Assert.That(initial.ElapsedSeconds, Is.Zero);
            Assert.That(runner.State.OxygenSaturation, Is.EqualTo(94));
            ScenarioLogEntry vital = runner.SessionLog.Entries.Last(e => e.EventType == "VITALS_CHANGE");
            Assert.That(vital.ElapsedSeconds, Is.EqualTo(2.5f));
            Assert.That(DateTime.TryParse(vital.TimestampUtc, out _), Is.True);
            Assert.That(runner.SessionLog.Entries.Select(e => e.Sequence),
                Is.EqualTo(Enumerable.Range(1, runner.SessionLog.Entries.Count)));
        }

        [Test]
        public void NonMatchingHotspot_IsLoggedWithoutInventingAChoice()
        {
            runner.ContinueMessage();
            Assert.That(runner.TryHandleHotspotInteraction("hs_ehr"), Is.False);
            Assert.That(runner.SessionLog.Entries.Count(e => e.EventType == "HOTSPOT_INTERACTION"), Is.EqualTo(1));
            Assert.That(runner.SessionLog.Entries.Any(e => e.EventType == "OPTION_SELECTED"), Is.False);
        }

        [Test]
        public void CompleteEscalationPath_ReportsSavedDocumentationAndRawScore()
        {
            ReachFirstGate();
            Save("assessment_form", new Dictionary<string, string> { ["observation"] = "Κυάνωση και ταχύπνοια" });
            Save("intervention_form", new Dictionary<string, string> { ["fiO2_setting"] = "100% (simulation)" });
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n5_reassessment"));
            runner.ContinueMessage();
            runner.TryHandleHotspotInteraction("hs_call");
            Save("communication_log", new Dictionary<string, string> { ["recipient"] = "Duty doctor", ["outcome"] = "Review requested" });

            ScenarioDebriefReport report = runner.SessionLog.BuildDebrief();
            Assert.That(report.Completed, Is.True);
            Assert.That(report.Score, Is.EqualTo(155));
            Assert.That(report.MissedDocumentation, Is.Empty);
            Assert.That(report.NotRequiredDocumentation, Is.Empty);
            Assert.That(report.SavedDocumentation.Count, Is.EqualTo(4));
            Assert.That(report.Checklist.All(item => item.IsComplete), Is.True);
            Assert.That(report.DecisionPath.Any(step => step.Contains("opt_call_doc")), Is.True);
            Assert.That(runner.SessionLog.Entries.Last().EventType, Is.EqualTo("SCENARIO_COMPLETED"));
            Assert.That(runner.SessionLog.EndedUtc, Is.Not.Null);
        }

        [Test]
        public void MonitorBranch_DistinguishesUnneededCommunicationFromMissingRequiredFields()
        {
            ReachFirstGate();
            Assert.That(runner.SessionLog.BuildDebrief().MissedDocumentation.Count, Is.EqualTo(2));
            Save("assessment_form", new Dictionary<string, string> { ["observation"] = "Observed" });
            Save("intervention_form", new Dictionary<string, string> { ["fiO2_setting"] = "100%" });
            runner.ContinueMessage();
            runner.TryHandleHotspotInteraction("hs_monitor");

            ScenarioDebriefReport report = runner.SessionLog.BuildDebrief();
            Assert.That(report.Completed, Is.True);
            Assert.That(report.MissedDocumentation, Is.Empty);
            Assert.That(report.NotRequiredDocumentation.Count, Is.EqualTo(2));
        }

        [Test]
        public void DebugBypass_IsVisibleAndDoesNotClaimSavedDocumentation()
        {
            ReachFirstGate();
            runner.DebugBypassCurrentGate();
            ScenarioDebriefReport report = runner.SessionLog.BuildDebrief();
            Assert.That(report.UsedDebugBypass, Is.True);
            Assert.That(report.MissedDocumentation.Count, Is.EqualTo(2));
            Assert.That(report.SavedDocumentation, Is.Empty);
            Assert.That(report.SummaryText, Does.Contain("DEVELOPMENT RUN"));
        }

        [Test]
        public void Timeout_IsRecordedOnceWithPenaltyAndReviewChecklist()
        {
            runner.ContinueMessage();
            runner.Tick(30f);
            runner.Tick(1f);
            Assert.That(runner.SessionLog.BuildDebrief().TimeoutCount, Is.EqualTo(1));
            Assert.That(runner.SessionLog.Entries.Any(e => e.EventType == "TIMEOUT" && e.ElapsedSeconds == 30f), Is.True);
            Assert.That(runner.SessionLog.BuildDebrief().Score, Is.EqualTo(90));
        }

        [Test]
        public void DocumentationLog_CopiesSubmittedValuesBeforeTheyAreEdited()
        {
            Dictionary<string, string> values = new Dictionary<string, string> { ["observation"] = "First entry" };
            runner.SessionLog.RecordDocumentation("assessment_form", values);
            values["observation"] = "Changed later";
            Assert.That(runner.SessionLog.Entries.Last().Fields["observation"], Is.EqualTo("First entry"));
        }

        [Test]
        public void JsonExport_RoundTripsUnicodeAndDoesNotOverwritePreviousExport()
        {
            string directory = Path.Combine(Path.GetTempPath(), "icu-log-test-" + Guid.NewGuid().ToString("N"));
            string first = null;
            string second = null;
            try
            {
                runner.SessionLog.RecordDocumentation("assessment_form", new Dictionary<string, string> { ["observation"] = "Έλεγχος ασθενούς" });
                first = runner.SessionLog.ExportJson(directory);
                second = runner.SessionLog.ExportJson(directory);
                JObject parsed = JObject.Parse(File.ReadAllText(first));
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(parsed["session_id"].Value<string>(), Is.EqualTo(runner.SessionLog.SessionId));
                Assert.That(parsed["events"].Last["Fields"]["observation"].Value<string>(), Is.EqualTo("Έλεγχος ασθενούς"));
                Assert.That(parsed["completed"].Value<bool>(), Is.False);
            }
            finally
            {
                if (first != null) File.Delete(first);
                if (second != null) File.Delete(second);
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
        }

        [Test]
        public void Stop_MarksIncompleteSessionAndNewStartUsesNewSessionId()
        {
            string previousId = runner.SessionLog.SessionId;
            runner.Stop();
            Assert.That(runner.SessionLog.Completed, Is.False);
            Assert.That(runner.SessionLog.EndedUtc, Is.Not.Null);
            Assert.That(runner.SessionLog.Entries.Last().EventType, Is.EqualTo("SCENARIO_STOPPED"));
            runner.Start();
            Assert.That(runner.SessionLog.SessionId, Is.Not.EqualTo(previousId));
            Assert.That(runner.SessionLog.EndedUtc, Is.Null);
            Assert.That(runner.SessionLog.Entries.Any(e => e.EventType == "SCENARIO_STOPPED"), Is.False);
        }

        [Test]
        public void Tick_IgnoresNonFiniteTimeToKeepExportsValid()
        {
            runner.Tick(float.NaN);
            runner.Tick(float.PositiveInfinity);
            Assert.That(runner.State.ElapsedTime, Is.Zero);
        }

        private void ReachFirstGate()
        {
            runner.ContinueMessage();
            runner.TryHandleHotspotInteraction("hs_patient");
            runner.TryHandleHotspotInteraction("hs_ventilator");
        }

        private void Save(string form, Dictionary<string, string> values)
        {
            documentation.SaveForm(form, values);
            runner.SessionLog.RecordDocumentation(form, documentation.GetFormValues(form));
            runner.EvaluateCurrentGate();
        }
    }
}
