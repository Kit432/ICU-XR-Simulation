using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ICUSimulation.Scenarios
{
    [Serializable]
    public sealed class ScenarioDefinition
    {
        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; }

        [JsonProperty("scenario_meta")]
        public ScenarioMetadata Metadata { get; set; }

        [JsonProperty("initial_state")]
        public ScenarioInitialState InitialState { get; set; }

        [JsonProperty("hotspots")]
        public List<ScenarioHotspot> Hotspots { get; set; }

        [JsonProperty("ehr_config")]
        public EhrConfiguration EhrConfiguration { get; set; }

        [JsonProperty("rules")]
        public ScenarioRules Rules { get; set; }

        [JsonProperty("nodes")]
        public List<ScenarioNode> Nodes { get; set; }

        [JsonProperty("logging")]
        public LoggingConfiguration Logging { get; set; }
    }

    [Serializable]
    public sealed class ScenarioMetadata
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("estimated_duration_minutes")] public int EstimatedDurationMinutes { get; set; }
        [JsonProperty("difficulty")] public string Difficulty { get; set; }
        [JsonProperty("learning_goals")] public List<string> LearningGoals { get; set; }
    }

    [Serializable]
    public sealed class ScenarioInitialState
    {
        [JsonProperty("time_elapsed")] public float TimeElapsed { get; set; }
        [JsonProperty("current_score")] public int CurrentScore { get; set; }
        [JsonProperty("flags")] public Dictionary<string, bool> Flags { get; set; }
        [JsonProperty("vitals")] public ScenarioVitals Vitals { get; set; }
        [JsonProperty("ui")] public ScenarioInitialUiState Ui { get; set; }
    }

    [Serializable]
    public sealed class ScenarioVitals
    {
        [JsonProperty("hr")] public int HeartRate { get; set; }
        [JsonProperty("spo2")] public int OxygenSaturation { get; set; }
        [JsonProperty("rr")] public int RespiratoryRate { get; set; }
        [JsonProperty("bp")] public string BloodPressure { get; set; }
        [JsonProperty("temp")] public float Temperature { get; set; }
    }

    [Serializable]
    public sealed class ScenarioInitialUiState
    {
        [JsonProperty("active_hotspots")] public List<string> ActiveHotspots { get; set; }
        [JsonProperty("monitor_alert")] public bool MonitorAlert { get; set; }
    }

    [Serializable]
    public sealed class ScenarioHotspot
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("label")] public string Label { get; set; }
    }

    [Serializable]
    public sealed class EhrConfiguration
    {
        [JsonProperty("forms")] public Dictionary<string, EhrFormDefinition> Forms { get; set; }
    }

    [Serializable]
    public sealed class EhrFormDefinition
    {
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("fields")] public List<string> Fields { get; set; }
    }

    [Serializable]
    public sealed class ScenarioRules
    {
        [JsonProperty("global_rules")] public List<GlobalRuleDefinition> GlobalRules { get; set; }
    }

    [Serializable]
    public sealed class GlobalRuleDefinition
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("condition")] public JObject Condition { get; set; }
        [JsonProperty("effects")] public List<ScenarioEffect> Effects { get; set; }
    }

    [Serializable]
    public sealed class ScenarioNode
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("next_node_id")] public string NextNodeId { get; set; }
        [JsonProperty("options")] public List<ScenarioOption> Options { get; set; }
        [JsonProperty("timeout")] public TimeoutDefinition Timeout { get; set; }
        [JsonProperty("gate_requirements")] public GateRequirements GateRequirements { get; set; }
        [JsonProperty("feedback_blocked")] public string FeedbackBlocked { get; set; }
        [JsonProperty("feedback_success")] public string FeedbackSuccess { get; set; }
        [JsonProperty("effects_on_pass")] public ScenarioEffectBundle EffectsOnPass { get; set; }
        [JsonProperty("debrief_config")] public DebriefConfiguration DebriefConfiguration { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    [Serializable]
    public sealed class ScenarioOption
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("label")] public string Label { get; set; }
        [JsonProperty("target_hotspot")] public string TargetHotspot { get; set; }
        [JsonProperty("effects")] public ScenarioEffectBundle Effects { get; set; }
        [JsonProperty("next_node_id")] public string NextNodeId { get; set; }
    }

    [Serializable]
    public sealed class TimeoutDefinition
    {
        [JsonProperty("seconds")] public float Seconds { get; set; }
        [JsonProperty("on_timeout_effects")] public ScenarioEffectBundle OnTimeoutEffects { get; set; }
        [JsonProperty("next_node_id")] public string NextNodeId { get; set; }
    }

    [Serializable]
    public sealed class GateRequirements
    {
        [JsonProperty("target_hotspot")] public string TargetHotspot { get; set; }
        [JsonProperty("required_forms")] public List<RequiredForm> RequiredForms { get; set; }
    }

    [Serializable]
    public sealed class RequiredForm
    {
        [JsonProperty("form_id")] public string FormId { get; set; }
        [JsonProperty("fields")] public List<string> Fields { get; set; }
    }

    [Serializable]
    public sealed class ScenarioEffectBundle
    {
        [JsonProperty("score_delta")] public int? ScoreDelta { get; set; }
        [JsonProperty("state_update")] public Dictionary<string, JToken> StateUpdate { get; set; }
        [JsonProperty("vitals_update")] public Dictionary<string, JToken> VitalsUpdate { get; set; }
        [JsonProperty("toast")] public string Toast { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    [Serializable]
    public sealed class ScenarioEffect
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("style")] public string Style { get; set; }
        [JsonProperty("message")] public string Message { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    [Serializable]
    public sealed class DebriefConfiguration
    {
        [JsonProperty("show_score")] public bool ShowScore { get; set; }
        [JsonProperty("show_decision_path")] public bool ShowDecisionPath { get; set; }
        [JsonProperty("highlight_missed_docs")] public bool HighlightMissedDocuments { get; set; }
        [JsonProperty("export_log")] public bool ExportLog { get; set; }
    }

    [Serializable]
    public sealed class LoggingConfiguration
    {
        [JsonProperty("enabled")] public bool Enabled { get; set; }
        [JsonProperty("log_events")] public List<string> LogEvents { get; set; }
        [JsonProperty("export_format")] public string ExportFormat { get; set; }
    }
}
