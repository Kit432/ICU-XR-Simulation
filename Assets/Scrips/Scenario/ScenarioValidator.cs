using System;
using System.Collections.Generic;

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
                    result.Warnings.Add($"Optional field '{path}.type' is empty.");
                }
                else if (!KnownNodeTypes.Contains(node.Type))
                {
                    result.Warnings.Add($"Node '{node.Id}' has unrecognized type '{node.Type}'. It was loaded but cannot be interpreted yet.");
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
                            result.Warnings.Add($"Node '{node.Id}' option at index {optionIndex} is null.");
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

        private static void RequireText(string value, string fieldPath, ScenarioValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add($"Required field '{fieldPath}' is missing or empty.");
            }
        }
    }
}
