using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioLogEntry
    {
        public int Sequence { get; set; }
        public string TimestampUtc { get; set; }
        public float ElapsedSeconds { get; set; }
        public string EventType { get; set; }
        public string NodeId { get; set; }
        public string SourceId { get; set; }
        public string Detail { get; set; }
        public ScenarioState State { get; set; }
        public Dictionary<string, string> Fields { get; set; }
    }

    public sealed class ScenarioChecklistItem
    {
        public string Label { get; set; }
        public bool IsComplete { get; set; }
        public string Detail { get; set; }
    }

    public sealed class ScenarioDebriefReport
    {
        public string ScenarioTitle { get; set; }
        public int Score { get; set; }
        public float ElapsedSeconds { get; set; }
        public bool Completed { get; set; }
        public IReadOnlyList<string> DecisionPath { get; set; }
        public IReadOnlyList<ScenarioChecklistItem> Checklist { get; set; }
        public IReadOnlyList<string> SavedDocumentation { get; set; }
        public IReadOnlyList<string> MissedDocumentation { get; set; }
        public IReadOnlyList<string> NotRequiredDocumentation { get; set; }
        public int TimeoutCount { get; set; }
        public bool UsedDebugBypass { get; set; }

        [JsonIgnore]
        public string SummaryText
        {
            get
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine(ScenarioTitle);
                text.AppendLine($"{(Completed ? "Completed" : "In progress / stopped")} | Score: {Score} points | Time: {ElapsedSeconds:0.0}s");
                text.AppendLine("Score is the scenario's raw points total, not a percentage.");
                if (UsedDebugBypass) text.AppendLine("DEVELOPMENT RUN: documentation was bypassed; this is not a valid training result.");
                text.AppendLine("\nCHECKLIST");
                foreach (ScenarioChecklistItem item in Checklist)
                    text.AppendLine($"{(item.IsComplete ? "[DONE]" : "[REVIEW]")} {item.Label}: {item.Detail}");
                text.AppendLine("\nDECISION PATH");
                foreach (string step in DecisionPath) text.AppendLine(step);
                text.AppendLine("\nSAVED DOCUMENTATION");
                text.AppendLine(SavedDocumentation.Count == 0 ? "No fields were saved." : string.Join("\n", SavedDocumentation));
                text.AppendLine("\nMISSING REQUIRED DOCUMENTATION");
                text.AppendLine(MissedDocumentation.Count == 0 ? "None on the path taken." : string.Join("\n", MissedDocumentation));
                if (NotRequiredDocumentation.Count > 0)
                {
                    text.AppendLine("\nNOT REQUIRED ON THIS PATH");
                    text.AppendLine(string.Join("\n", NotRequiredDocumentation));
                }
                return text.ToString().TrimEnd();
            }
        }
    }

    /// <summary>One run's immutable event snapshots, saved EHR values and debrief evidence.</summary>
    public sealed class ScenarioSessionLog
    {
        private readonly ScenarioRunner runner;
        private readonly List<ScenarioLogEntry> entries = new List<ScenarioLogEntry>();
        private readonly DocumentationStore documentation = new DocumentationStore();

        public string SessionId { get; private set; }
        public string StartedUtc { get; private set; }
        public string EndedUtc { get; private set; }
        public bool Completed { get; private set; }
        public IReadOnlyList<ScenarioLogEntry> Entries => entries.AsReadOnly();

        internal ScenarioSessionLog(ScenarioRunner runner)
        {
            this.runner = runner;
            runner.ScenarioStarted += Begin;
            runner.NodeEntered += node => Record("NODE_ENTER", node.Id, node.Text);
            runner.OptionSelected += choice => Record("OPTION_SELECTED", choice.Option.Id,
                choice.Option.Label, new Dictionary<string, string> { ["hotspot_id"] = choice.HotspotId });
            runner.VitalsChanged += change => Record("VITALS_CHANGE", change.VitalName,
                $"{Convert.ToString(change.PreviousValue, CultureInfo.InvariantCulture)} -> {Convert.ToString(change.CurrentValue, CultureInfo.InvariantCulture)}");
            runner.ScoreChanged += change => Record("SCORE_CHANGE", null, $"{change.PreviousScore} -> {change.CurrentScore}");
            runner.FlagChanged += change => Record("FLAG_CHANGE", change.FlagName, change.CurrentValue.ToString());
            runner.TimeoutTriggered += timeout => Record("TIMEOUT", timeout.Node.Id, $"Limit: {timeout.ConfiguredSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
            runner.GlobalRuleActivated += rule => Record("GLOBAL_RULE_ACTIVATED", rule.Rule.Id);
            runner.GlobalRuleDeactivated += rule => Record("GLOBAL_RULE_DEACTIVATED", rule.Rule.Id);
            runner.GateBlocked += gate => Record("GATE_BLOCKED", gate.Node.Id, string.Join(", ", gate.Evaluation.MissingRequirements));
            runner.GatePassed += gate => Record(gate.WasDebugBypassed ? "GATE_DEBUG_BYPASS" : "GATE_PASSED", gate.Node.Id);
            runner.ScenarioCompleted += state =>
            {
                Completed = true;
                EndedUtc = UtcNow();
                Record("SCENARIO_COMPLETED", state.CurrentNodeId);
            };
        }

        private void Begin(ScenarioRunner activeRunner)
        {
            entries.Clear();
            documentation.ResetForScenario(runner.Definition.Metadata.Id);
            SessionId = Guid.NewGuid().ToString("N");
            StartedUtc = UtcNow();
            EndedUtc = null;
            Completed = false;
            Record("SCENARIO_STARTED", runner.Definition.Metadata.Id);
        }

        public void RecordInteraction(string hotspotId, string detail = null)
        {
            if (runner.IsRunning) Record("HOTSPOT_INTERACTION", hotspotId, detail);
        }

        public void RecordDocumentation(string formId, IReadOnlyDictionary<string, string> values)
        {
            if (!runner.IsRunning || string.IsNullOrWhiteSpace(formId)) return;
            documentation.SaveForm(formId, values);
            Record("EHR_SUBMIT", formId, EhrDisplayNames.GetFormTitle(runner.Definition, formId),
                new Dictionary<string, string>(documentation.GetFormValues(formId), StringComparer.Ordinal));
        }

        internal void RecordStopped()
        {
            if (!runner.IsRunning || SessionId == null) return;
            EndedUtc = UtcNow();
            Record("SCENARIO_STOPPED", runner.State.CurrentNodeId);
        }

        internal void Record(string eventType, string sourceId = null, string detail = null,
            Dictionary<string, string> fields = null)
        {
            if (SessionId == null) return;
            entries.Add(new ScenarioLogEntry
            {
                Sequence = entries.Count + 1,
                TimestampUtc = UtcNow(),
                ElapsedSeconds = runner.State.ElapsedTime,
                EventType = eventType,
                NodeId = runner.State.CurrentNodeId,
                SourceId = sourceId,
                Detail = detail,
                State = runner.State.DeepCopy(),
                Fields = fields == null ? null : new Dictionary<string, string>(fields, StringComparer.Ordinal)
            });
        }

        public ScenarioDebriefReport BuildDebrief()
        {
            HashSet<string> visited = new HashSet<string>(entries.Where(e => e.EventType == "NODE_ENTER").Select(e => e.SourceId));
            List<string> saved = new List<string>();
            List<string> missing = new List<string>();
            List<string> notRequired = new List<string>();
            List<ScenarioChecklistItem> checklist = new List<ScenarioChecklistItem>();
            bool bypassed = entries.Any(e => e.EventType == "GATE_DEBUG_BYPASS");

            foreach (ScenarioNode gate in runner.Definition.Nodes.Where(n => n.GateRequirements?.RequiredForms != null))
            {
                foreach (RequiredForm form in gate.GateRequirements.RequiredForms)
                {
                    IEnumerable<string> requiredFields = form.Fields != null && form.Fields.Count > 0
                        ? form.Fields : new[] { string.Empty };
                    foreach (string field in requiredFields)
                    {
                        string label = EhrDisplayNames.GetFormTitle(runner.Definition, form.FormId) +
                            (string.IsNullOrEmpty(field) ? string.Empty : " / " + EhrDisplayNames.GetFieldLabel(field));
                        if (!visited.Contains(gate.Id)) { notRequired.Add(label); continue; }
                        bool hasValue = documentation.HasFieldValue(form.FormId, field);
                        if (!hasValue) missing.Add(label);
                        checklist.Add(new ScenarioChecklistItem { Label = label, IsComplete = hasValue,
                            Detail = hasValue ? "Required documentation saved" : "Required documentation missing" });
                    }
                }
            }

            if (runner.Definition.EhrConfiguration?.Forms != null)
                foreach (KeyValuePair<string, EhrFormDefinition> form in runner.Definition.EhrConfiguration.Forms)
                    foreach (KeyValuePair<string, string> field in documentation.GetFormValues(form.Key))
                        saved.Add($"{EhrDisplayNames.GetFormTitle(runner.Definition, form.Key)} / {EhrDisplayNames.GetFieldLabel(field.Key)}: {field.Value}");

            foreach (KeyValuePair<string, bool> flag in runner.State.Flags.Where(f => !f.Key.StartsWith("documentation_", StringComparison.Ordinal)))
                checklist.Insert(0, new ScenarioChecklistItem { Label = EhrDisplayNames.GetFieldLabel(flag.Key),
                    IsComplete = flag.Value, Detail = flag.Value ? "Recorded by the scenario" : "Not performed on this path; review the decision taken" });

            int timeouts = entries.Count(e => e.EventType == "TIMEOUT");
            checklist.Add(new ScenarioChecklistItem { Label = "Response within the decision time limit", IsComplete = timeouts == 0,
                Detail = timeouts == 0 ? "No timeout" : $"{timeouts} decision timeout(s)" });
            checklist.Add(new ScenarioChecklistItem { Label = "Scenario completed", IsComplete = Completed && !bypassed,
                Detail = bypassed ? "Development bypass used" : Completed ? "End node reached" : "End node not yet reached" });

            return new ScenarioDebriefReport
            {
                ScenarioTitle = runner.Definition.Metadata.Title, Score = runner.State.CurrentScore,
                ElapsedSeconds = runner.State.ElapsedTime, Completed = Completed, TimeoutCount = timeouts,
                UsedDebugBypass = bypassed, Checklist = checklist.AsReadOnly(),
                SavedDocumentation = saved.AsReadOnly(), MissedDocumentation = missing.Distinct().ToList().AsReadOnly(),
                NotRequiredDocumentation = notRequired.Distinct().ToList().AsReadOnly(),
                DecisionPath = entries.Where(e => e.EventType == "NODE_ENTER" || e.EventType == "OPTION_SELECTED" || e.EventType == "TIMEOUT")
                    .Select(e => $"{e.ElapsedSeconds:0.0}s | {e.EventType} | {e.SourceId}: {e.Detail}").ToList().AsReadOnly()
            };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(new
            {
                schema_version = "1.0", session_id = SessionId, scenario_id = runner.Definition.Metadata.Id,
                scenario_title = runner.Definition.Metadata.Title, started_utc = StartedUtc, ended_utc = EndedUtc,
                completed = Completed, debrief = BuildDebrief(), events = entries
            }, Formatting.Indented);
        }

        public string ExportJson(string directory)
        {
            if (SessionId == null) throw new InvalidOperationException("Start a scenario before exporting its session.");
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An export directory is required.", nameof(directory));
            Directory.CreateDirectory(directory);
            // The file name uses generated identifiers only; scenario JSON cannot write outside the export directory.
            string path = Path.Combine(directory, "icu-session-" + SessionId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
            return Path.GetFullPath(path);
        }

        private static string UtcNow() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }
}
