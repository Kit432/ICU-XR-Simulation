using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;

        public string FormatErrors()
        {
            return string.Join(Environment.NewLine, Errors);
        }
    }

    public static class ScenarioValidator
    {
        private static readonly HashSet<string> KnownNodeTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "message", "decision", "gate", "end"
            };

        public static ScenarioValidationResult Validate(ScenarioDefinition definition)
        {
            ScenarioValidationResult result = new ScenarioValidationResult();

            if (definition == null)
            {
                result.Errors.Add("Scenario JSON did not contain a scenario object.");
                return result;
            }

            RequireText(definition.SchemaVersion, "schema_version", result);

            if (definition.Metadata == null)
            {
                result.Errors.Add("Required object 'scenario_meta' is missing.");
            }
            else
            {
                RequireText(definition.Metadata.Id, "scenario_meta.id", result);
                RequireText(definition.Metadata.Title, "scenario_meta.title", result);

                if (string.IsNullOrWhiteSpace(definition.Metadata.Description))
                {
                    result.Warnings.Add("Optional field 'scenario_meta.description' is empty.");
                }

                if (definition.Metadata.LearningGoals == null || definition.Metadata.LearningGoals.Count == 0)
                {
                    result.Warnings.Add("Optional field 'scenario_meta.learning_goals' is empty.");
                }
            }

            if (definition.InitialState == null)
            {
                result.Errors.Add("Required object 'initial_state' is missing.");
            }
            else if (definition.InitialState.Vitals == null)
            {
                result.Errors.Add("Required object 'initial_state.vitals' is missing.");
            }

            if (definition.Hotspots == null)
            {
                result.Errors.Add("Required array 'hotspots' is missing.");
            }
            else if (definition.Hotspots.Count == 0)
            {
                result.Warnings.Add("The scenario has no configured hotspots.");
            }

            if (definition.Nodes == null)
            {
                result.Errors.Add("Required array 'nodes' is missing.");
                return result;
            }

            if (definition.Nodes.Count == 0)
            {
                result.Errors.Add("Required array 'nodes' must contain at least one node.");
                return result;
            }

            ValidateNodes(definition.Nodes, result);
            ValidateStructure(definition, result);
            ValidateRuleAndEffectValues(definition, result);

            if (definition.EhrConfiguration == null)
            {
                result.Warnings.Add("Optional object 'ehr_config' is missing.");
            }

            if (definition.Rules == null)
            {
                result.Warnings.Add("Optional object 'rules' is missing.");
            }

            if (definition.Logging == null)
            {
                result.Warnings.Add("Optional object 'logging' is missing.");
            }

            return result;
        }

        private static void ValidateNodes(IReadOnlyList<ScenarioNode> nodes, ScenarioValidationResult result)
        {
            HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < nodes.Count; index++)
            {
                ScenarioNode node = nodes[index];
                if (node == null)
                {
                    result.Errors.Add($"nodes[{index}] is null.");
                    continue;
                }

                string path = $"nodes[{index}]";
                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    result.Errors.Add($"Required field '{path}.id' is empty.");
                }
                else if (!nodeIds.Add(node.Id))
                {
                    result.Errors.Add($"Duplicate node id '{node.Id}' was found.");
                }

                if (string.IsNullOrWhiteSpace(node.Type))
                {
                    result.Errors.Add($"Required field '{path}.type' is empty.");
                }
                else if (!KnownNodeTypes.Contains(node.Type))
                {
                    result.Errors.Add($"Node '{node.Id}' has unrecognized type '{node.Type}'. Supported types: message, decision, gate, end.");
                }
            }

            for (int index = 0; index < nodes.Count; index++)
            {
                ScenarioNode node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                ValidateReference(node.NextNodeId, $"node '{node.Id}' next_node_id", nodeIds, result);

                if (node.Options != null)
                {
                    for (int optionIndex = 0; optionIndex < node.Options.Count; optionIndex++)
                    {
                        ScenarioOption option = node.Options[optionIndex];
                        if (option == null)
                        {
                            result.Errors.Add($"Node '{node.Id}' option at index {optionIndex} is null.");
                            continue;
                        }

                        ValidateReference(
                            option.NextNodeId,
                            $"node '{node.Id}' option '{option.Id ?? optionIndex.ToString()}' next_node_id",
                            nodeIds,
                            result);
                    }
                }

                if (node.Timeout != null)
                {
                    ValidateReference(
                        node.Timeout.NextNodeId,
                        $"node '{node.Id}' timeout next_node_id",
                        nodeIds,
                        result);
                }
            }
        }

        private static void ValidateStructure(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            HashSet<string> hotspots = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScenarioHotspot hotspot in definition.Hotspots ?? new List<ScenarioHotspot>())
            {
                if (hotspot == null || string.IsNullOrWhiteSpace(hotspot.Id))
                    result.Errors.Add("Every hotspot needs a non-empty id.");
                else if (!hotspots.Add(hotspot.Id)) result.Errors.Add($"Duplicate hotspot id '{hotspot.Id}'.");
            }

            foreach (string active in definition.InitialState?.Ui?.ActiveHotspots ?? new List<string>())
                if (!hotspots.Contains(active)) result.Errors.Add($"Active hotspot '{active}' is not configured in hotspots.");

            if (definition.InitialState != null && (definition.InitialState.TimeElapsed < 0 ||
                float.IsNaN(definition.InitialState.TimeElapsed) || float.IsInfinity(definition.InitialState.TimeElapsed)))
                result.Errors.Add("initial_state.time_elapsed must be a finite non-negative number.");

            Dictionary<string, EhrFormDefinition> forms = definition.EhrConfiguration?.Forms;
            if (forms != null)
                foreach (KeyValuePair<string, EhrFormDefinition> form in forms)
                {
                    if (string.IsNullOrWhiteSpace(form.Key) || form.Value == null)
                    {
                        result.Errors.Add("Every EHR form needs a non-empty id and a form object.");
                        continue;
                    }
                    if (form.Value.Fields == null || form.Value.Fields.Count == 0)
                        result.Errors.Add($"EHR form '{form.Key}' must define at least one field.");
                    else if (form.Value.Fields.Any(string.IsNullOrWhiteSpace) || form.Value.Fields.Distinct().Count() != form.Value.Fields.Count)
                        result.Errors.Add($"EHR form '{form.Key}' contains empty or duplicate field ids.");
                }

            foreach (ScenarioNode node in definition.Nodes.Where(n => n != null))
            {
                bool gate = string.Equals(node.Type, "gate", StringComparison.OrdinalIgnoreCase);
                bool decision = string.Equals(node.Type, "decision", StringComparison.OrdinalIgnoreCase);
                if (gate || string.Equals(node.Type, "message", StringComparison.OrdinalIgnoreCase))
                    RequireText(node.NextNodeId, $"node '{node.Id}' next_node_id", result);

                if (decision && (node.Options == null || node.Options.Count == 0))
                    result.Errors.Add($"Decision node '{node.Id}' needs at least one option.");

                HashSet<string> optionIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> optionHotspots = new HashSet<string>(StringComparer.Ordinal);
                foreach (ScenarioOption option in node.Options ?? new List<ScenarioOption>())
                {
                    if (option == null) continue;
                    RequireText(option.Id, $"node '{node.Id}' option id", result);
                    RequireText(option.Label, $"node '{node.Id}' option '{option.Id}' label", result);
                    RequireText(option.NextNodeId, $"node '{node.Id}' option '{option.Id}' next_node_id", result);
                    if (!optionIds.Add(option.Id ?? string.Empty)) result.Errors.Add($"Duplicate option id '{option.Id}' in node '{node.Id}'.");
                    if (string.IsNullOrWhiteSpace(option.TargetHotspot) || !hotspots.Contains(option.TargetHotspot))
                        result.Errors.Add($"Option '{option.Id}' target hotspot '{option.TargetHotspot}' is not configured.");
                    else if (!optionHotspots.Add(option.TargetHotspot))
                        result.Errors.Add($"Node '{node.Id}' assigns multiple options to hotspot '{option.TargetHotspot}'; the choice is ambiguous.");
                }

                if (node.Timeout != null)
                {
                    if (node.Timeout.Seconds <= 0f || float.IsNaN(node.Timeout.Seconds) || float.IsInfinity(node.Timeout.Seconds))
                        result.Errors.Add($"Node '{node.Id}' timeout seconds must be a finite positive number.");
                    RequireText(node.Timeout.NextNodeId, $"node '{node.Id}' timeout next_node_id", result);
                }

                if (!gate) continue;
                if (node.GateRequirements?.RequiredForms == null || node.GateRequirements.RequiredForms.Count == 0)
                {
                    result.Errors.Add($"Gate '{node.Id}' must declare required_forms.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.GateRequirements.TargetHotspot) || !hotspots.Contains(node.GateRequirements.TargetHotspot))
                    result.Errors.Add($"Gate '{node.Id}' target hotspot is not configured.");
                foreach (RequiredForm required in node.GateRequirements.RequiredForms)
                {
                    if (required == null || string.IsNullOrWhiteSpace(required.FormId) || forms == null ||
                        !forms.TryGetValue(required.FormId, out EhrFormDefinition form) || form == null)
                    {
                        result.Errors.Add($"Gate '{node.Id}' references an unknown EHR form '{required?.FormId}'.");
                        continue;
                    }
                    foreach (string field in required.Fields ?? new List<string>())
                        if (string.IsNullOrWhiteSpace(field) || form.Fields == null || !form.Fields.Contains(field))
                            result.Errors.Add($"Gate '{node.Id}' references unknown EHR field '{required.FormId}.{field}'.");
                }
            }

            // Gates advance synchronously when saved fields satisfy them. A gate-only cycle would recurse forever.
            Dictionary<string, ScenarioNode> nodes = definition.Nodes.Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id))
                .GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (ScenarioNode node in nodes.Values.Where(n => string.Equals(n.Type, "gate", StringComparison.OrdinalIgnoreCase)))
            {
                HashSet<string> chain = new HashSet<string>(StringComparer.Ordinal);
                ScenarioNode current = node;
                while (current != null && string.Equals(current.Type, "gate", StringComparison.OrdinalIgnoreCase))
                {
                    if (!chain.Add(current.Id))
                    {
                        result.Errors.Add($"Gate cycle starting at '{node.Id}' can advance forever without user input.");
                        break;
                    }
                    nodes.TryGetValue(current.NextNodeId ?? string.Empty, out current);
                }
            }

            if (!nodes.Values.Any(n => string.Equals(n.Type, "end", StringComparison.OrdinalIgnoreCase)))
                result.Errors.Add("The scenario must include an end node.");
            else if (definition.Nodes[0] != null)
            {
                HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
                Queue<string> pending = new Queue<string>();
                pending.Enqueue(definition.Nodes[0].Id ?? string.Empty);
                while (pending.Count > 0)
                {
                    string id = pending.Dequeue();
                    if (!reachable.Add(id) || !nodes.TryGetValue(id, out ScenarioNode node)) continue;
                    if (!string.IsNullOrWhiteSpace(node.NextNodeId)) pending.Enqueue(node.NextNodeId);
                    if (!string.IsNullOrWhiteSpace(node.Timeout?.NextNodeId)) pending.Enqueue(node.Timeout.NextNodeId);
                    foreach (ScenarioOption option in node.Options ?? new List<ScenarioOption>())
                        if (!string.IsNullOrWhiteSpace(option?.NextNodeId)) pending.Enqueue(option.NextNodeId);
                }
                if (!nodes.Values.Any(n => reachable.Contains(n.Id) && string.Equals(n.Type, "end", StringComparison.OrdinalIgnoreCase)))
                    result.Errors.Add("No end node can be reached from the first node.");
            }
        }

        private static void ValidateReference(
            string referencedId,
            string sourceDescription,
            HashSet<string> validIds,
            ScenarioValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(referencedId) && !validIds.Contains(referencedId))
            {
                result.Errors.Add($"Invalid {sourceDescription}: referenced node '{referencedId}' does not exist.");
            }
        }

        private static void ValidateRuleAndEffectValues(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            foreach (GlobalRuleDefinition rule in definition.Rules?.GlobalRules ?? new List<GlobalRuleDefinition>())
            {
                if (rule == null || rule.Condition == null)
                {
                    result.Errors.Add("Every global rule must have a condition object.");
                    continue;
                }
                foreach (JProperty condition in rule.Condition.Properties())
                {
                    string source = $"Global rule '{rule.Id}' condition '{condition.Name}'";
                    if (!IsConditionPath(condition.Name))
                    {
                        result.Errors.Add($"{source} uses an unsupported state path.");
                        continue;
                    }
                    if (condition.Value is JObject comparisons)
                    {
                        foreach (JProperty comparison in comparisons.Properties())
                        {
                            string op = comparison.Name.Trim().ToLowerInvariant();
                            if (op != "eq" && op != "neq" && op != "lt" && op != "lte" && op != "gt" && op != "gte")
                                result.Errors.Add($"{source} uses unsupported comparison '{comparison.Name}'.");
                            else if (op != "eq" && op != "neq" && !IsNumericPath(condition.Name))
                                result.Errors.Add($"{source} requires eq or neq for a non-numeric value.");
                            ValidateValue(condition.Name, comparison.Value, source, result);
                        }
                    }
                    else ValidateValue(condition.Name, condition.Value, source, result);
                }
            }

            foreach (ScenarioNode node in definition.Nodes.Where(n => n != null))
            {
                ValidateBundle(node.EffectsOnPass, $"Gate '{node.Id}' effects_on_pass", result);
                ValidateBundle(node.Timeout?.OnTimeoutEffects, $"Node '{node.Id}' timeout effects", result);
                foreach (ScenarioOption option in node.Options ?? new List<ScenarioOption>())
                    if (option != null) ValidateBundle(option.Effects, $"Option '{option.Id}' effects", result);
            }
        }

        private static void ValidateBundle(ScenarioEffectBundle bundle, string source, ScenarioValidationResult result)
        {
            if (bundle == null) return;
            foreach (KeyValuePair<string, JToken> value in bundle.StateUpdate ?? new Dictionary<string, JToken>())
            {
                if (value.Key == "ui.active_hotspots")
                {
                    if (!(value.Value is JArray ids) || ids.Any(id => id.Type != JTokenType.String || string.IsNullOrWhiteSpace(id.Value<string>())))
                        result.Errors.Add($"{source} ui.active_hotspots requires an array of non-empty string ids.");
                }
                else if (IsConditionPath(value.Key)) ValidateValue(value.Key, value.Value, source, result);
            }
            foreach (KeyValuePair<string, JToken> value in bundle.VitalsUpdate ?? new Dictionary<string, JToken>())
            {
                string path = value.Key.StartsWith("vitals.", StringComparison.Ordinal) ? value.Key : "vitals." + value.Key;
                if (IsConditionPath(path)) ValidateValue(path, value.Value, source, result);
            }
        }

        private static bool IsConditionPath(string path) =>
            (path.StartsWith("flags.", StringComparison.Ordinal) && path.Length > 6) ||
            path == "ui.monitor_alert" || path == "vitals.bp" || IsNumericPath(path);

        private static bool IsNumericPath(string path) => path == "vitals.hr" || path == "vitals.spo2" ||
            path == "vitals.rr" || path == "vitals.temp" || path == "score" || path == "current_score" || path == "time_elapsed";

        private static void ValidateValue(string path, JToken value, string source, ScenarioValidationResult result)
        {
            if (!(value is JValue) || value.Type == JTokenType.Null || value.Type == JTokenType.Undefined)
            {
                result.Errors.Add($"{source} '{path}' requires a scalar value, not null, an object or an array.");
                return;
            }
            if (path.StartsWith("flags.", StringComparison.Ordinal) || path == "ui.monitor_alert")
            {
                if (value.Type != JTokenType.Boolean && (value.Type != JTokenType.String || !bool.TryParse(value.Value<string>(), out _)))
                    result.Errors.Add($"{source} '{path}' requires true or false.");
            }
            else if (IsNumericPath(path))
            {
                bool numeric = (value.Type == JTokenType.Integer || value.Type == JTokenType.Float || value.Type == JTokenType.String) &&
                    double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                    !double.IsNaN(number) && !double.IsInfinity(number);
                if (!numeric) result.Errors.Add($"{source} '{path}' requires a finite numeric value.");
            }
            else if (path == "vitals.bp" && value.Type != JTokenType.String)
                result.Errors.Add($"{source} '{path}' requires a string value.");
        }

        private static void RequireText(string value, string fieldPath, ScenarioValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add($"Required field '{fieldPath}' is missing or empty.");
            }
        }
    }
}
