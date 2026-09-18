using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using ICUSimulation.Scenarios;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ICUSimulation.Tests.EditMode
{
    public sealed class ScenarioDataTests
    {
        private string samplePath;
        private ScenarioLoader loader;

        [SetUp]
        public void SetUp()
        {
            samplePath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Scenarios",
                "icu_scenario_hypoxia_v1.json");
            loader = new ScenarioLoader();
        }

        [Test]
        public void UniversityScenario_Deserializes()
        {
            ScenarioLoadResult result = loader.LoadFromFile(samplePath);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Definition, Is.Not.Null);
        }

        [Test]
        public void UniversityScenario_Validates()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            ScenarioValidationResult validation = ScenarioValidator.Validate(definition);

            Assert.That(validation.IsValid, Is.True, validation.FormatErrors());
        }

        [Test]
        public void UniversityScenario_MetadataIsReadCorrectly()
        {
            ScenarioMetadata metadata = LoadUniversityScenario().Metadata;

            Assert.That(metadata.Id, Is.EqualTo("icu_scenario_hypoxia_v1"));
            Assert.That(metadata.Title, Is.EqualTo("ΜΕΘ: Διαχείριση Υποξαιμίας & Αναπνευστήρα"));
            Assert.That(metadata.EstimatedDurationMinutes, Is.EqualTo(8));
        }

        [Test]
        public void UniversityScenario_InitialVitalsAreCorrect()
        {
            ScenarioVitals vitals = LoadUniversityScenario().InitialState.Vitals;

            Assert.That(vitals.HeartRate, Is.EqualTo(110));
            Assert.That(vitals.OxygenSaturation, Is.EqualTo(88));
            Assert.That(vitals.RespiratoryRate, Is.EqualTo(24));
            Assert.That(vitals.BloodPressure, Is.EqualTo("125/80"));
            Assert.That(vitals.Temperature, Is.EqualTo(37f));
        }

        [Test]
        public void UniversityScenario_InitialFlagsAreCorrect()
        {
            Dictionary<string, bool> flags = LoadUniversityScenario().InitialState.Flags;

            Assert.That(flags.Keys, Is.EquivalentTo(new[]
            {
                "assessment_complete",
                "oxygen_adjusted",
                "documentation_1_complete",
                "escalation_complete",
                "documentation_2_complete"
            }));
            Assert.That(flags.Values, Has.All.False);
        }

        [Test]
        public void UniversityScenario_AllEightNodeIdsAreDiscovered()
        {
            List<string> nodeIds = LoadUniversityScenario().Nodes.Select(node => node.Id).ToList();

            Assert.That(nodeIds, Is.EqualTo(new[]
            {
                "n1_start",
                "n2_initial_decision",
                "n3_intervention",
                "n4_gate_documentation_1",
                "n5_reassessment",
                "n6_escalation_decision",
                "n7_gate_documentation_2",
                "n8_end_scenario"
            }));
        }

        [Test]
        public void DuplicateNodeId_FailsValidation()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[1].Id = definition.Nodes[0].Id;

            ScenarioValidationResult validation = ScenarioValidator.Validate(definition);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.Contains("Duplicate node id")), Is.True);
        }

        [Test]
        public void BrokenNextNodeReference_FailsValidation()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[0].NextNodeId = "missing_node";

            ScenarioValidationResult validation = ScenarioValidator.Validate(definition);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(error => error.Contains("missing_node")), Is.True);
        }

        [Test]
        public void ResetScenario_RecreatesOriginalState()
        {
            ScenarioSession session = new ScenarioSession();
            ScenarioState state = session.LoadScenario(LoadUniversityScenario());
            state.CurrentScore = 50;
            state.OxygenSaturation = 70;
            state.Flags["assessment_complete"] = true;

            ScenarioState reset = session.ResetScenario();

            Assert.That(reset.CurrentScore, Is.EqualTo(100));
            Assert.That(reset.OxygenSaturation, Is.EqualTo(88));
            Assert.That(reset.Flags["assessment_complete"], Is.False);
            Assert.That(reset.CurrentNodeId, Is.EqualTo("n1_start"));
        }

        [Test]
        public void RuntimeMutation_DoesNotMutateDefinition()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            ScenarioSession session = new ScenarioSession();
            ScenarioState state = session.LoadScenario(definition);
            state.CurrentScore = 50;
            state.OxygenSaturation = 70;
            state.Flags["assessment_complete"] = true;
            session.ResetScenario();

            Assert.That(definition.InitialState.CurrentScore, Is.EqualTo(100));
            Assert.That(definition.InitialState.Vitals.OxygenSaturation, Is.EqualTo(88));
            Assert.That(definition.InitialState.Flags["assessment_complete"], Is.False);
        }

        [Test]
        public void Discovery_FindsMultipleValidJsonFilesWithoutHardcodedNames()
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "icu-scenario-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                string firstJson = File.ReadAllText(samplePath);
                string secondJson = firstJson
                    .Replace("\"id\": \"icu_scenario_hypoxia_v1\"", "\"id\": \"another_scenario\"")
                    .Replace(
                        "\"title\": \"ΜΕΘ: Διαχείριση Υποξαιμίας & Αναπνευστήρα\"",
                        "\"title\": \"Another Valid Scenario\"");
                File.WriteAllText(Path.Combine(temporaryDirectory, "first.json"), firstJson);
                File.WriteAllText(Path.Combine(temporaryDirectory, "another_scenario.json"), secondJson);

                ScenarioDiscoveryResult discovery = new ScenarioLoader(temporaryDirectory).DiscoverScenarios();

                Assert.That(discovery.Errors, Is.Empty);
                Assert.That(discovery.Scenarios.Count, Is.EqualTo(2));
                Assert.That(discovery.Scenarios.Select(item => item.Title), Does.Contain("Another Valid Scenario"));
            }
            finally
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void MalformedJson_FailsGracefully()
        {
            ScenarioLoadResult result = loader.LoadFromJson("{ invalid json");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.StartWith("Malformed scenario JSON:"));
        }

        [TestCase(null)]
        [TestCase("unsupported")]
        public void MissingOrUnsupportedNodeType_FailsBeforeRuntime(string type)
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[0].Type = type;
            Assert.That(ScenarioValidator.Validate(definition).IsValid, Is.False);
        }

        [Test]
        public void MissingMessageTransition_FailsBeforeRuntime()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[0].NextNodeId = null;
            Assert.That(ScenarioValidator.Validate(definition).IsValid, Is.False);
        }

        [Test]
        public void EmptyDecisionOrUnknownHotspot_FailsBeforeRuntime()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[1].Options[0].TargetHotspot = "hs_missing";
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("hs_missing")), Is.True);
            definition.Nodes[1].Options.Clear();
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("at least one option")), Is.True);
        }

        [Test]
        public void MultipleOptionsSharingHotspot_FailsAsAmbiguous()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[1].Options[1].TargetHotspot = definition.Nodes[1].Options[0].TargetHotspot;
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("ambiguous")), Is.True);
        }

        [Test]
        public void UnknownGateFormOrField_FailsBeforeItCanBlockForever()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            RequiredForm requirement = definition.Nodes[3].GateRequirements.RequiredForms[0];
            requirement.Fields[0] = "missing_field";
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("missing_field")), Is.True);
            requirement.FormId = "missing_form";
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("missing_form")), Is.True);
        }

        [Test]
        public void GateOnlyCycle_FailsBeforeSynchronousInfiniteRecursion()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[3].NextNodeId = definition.Nodes[3].Id;
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("Gate cycle")), Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void InvalidTimeout_FailsValidation(float seconds)
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[1].Timeout.Seconds = seconds;
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("finite positive")), Is.True);
        }

        [Test]
        public void UnreachableEnd_FailsValidation()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[6].NextNodeId = definition.Nodes[0].Id;
            definition.Nodes[5].Options[1].NextNodeId = definition.Nodes[0].Id;
            Assert.That(ScenarioValidator.Validate(definition).Errors.Any(e => e.Contains("No end node can be reached")), Is.True);
        }

        [Test]
        public void ReassessmentScenario_IsDistinctValidAndSelectable()
        {
            string secondPath = Path.Combine(Path.GetDirectoryName(samplePath), "icu_scenario_reassessment_v2.json");
            ScenarioLoadResult second = loader.LoadFromFile(secondPath);
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(second.Definition.Metadata.Id, Is.Not.EqualTo(LoadUniversityScenario().Metadata.Id));
            Assert.That(second.Definition.Nodes[1].Timeout.Seconds, Is.EqualTo(45f));
            Assert.That(loader.DiscoverScenarios().Scenarios.Any(s => s.Id == second.Definition.Metadata.Id), Is.True);
        }

        [TestCase("\"not-a-bool\"")]
        [TestCase("{\"nested\":true}")]
        [TestCase("[true]")]
        [TestCase("null")]
        public void MalformedBooleanRuleOperand_IsRejectedAtLoadAndSafeToEvaluate(string operandJson)
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            JObject condition = new JObject
            {
                ["flags.assessment_complete"] = new JObject { ["eq"] = JToken.Parse(operandJson) }
            };
            definition.Rules.GlobalRules[0].Condition = condition;
            Assert.That(ScenarioValidator.Validate(definition).IsValid, Is.False);
            Assert.That(new RuleEvaluator().EvaluateCondition(condition, state), Is.False);
        }

        [Test]
        public void UnknownRuleOperatorOrPath_IsRejectedWithClearValidationError()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Rules.GlobalRules[0].Condition = JObject.Parse("{\"vitals.spo2\":{\"approximately\":90},\"unknown.path\":true}");
            ScenarioValidationResult result = ScenarioValidator.Validate(definition);
            Assert.That(result.Errors.Any(e => e.Contains("unsupported comparison")), Is.True);
            Assert.That(result.Errors.Any(e => e.Contains("unsupported state path")), Is.True);
        }

        [Test]
        public void MalformedEffectOperands_AreRejectedBeforeAnActionCanSilentlyFail()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            definition.Nodes[1].Options[0].Effects.StateUpdate["flags.assessment_complete"] = "not-a-bool";
            definition.Nodes[2].Options[0].Effects.VitalsUpdate["spo2"] = new JObject { ["nested"] = 94 };
            ScenarioValidationResult result = ScenarioValidator.Validate(definition);
            Assert.That(result.Errors.Any(e => e.Contains("requires true or false")), Is.True);
            Assert.That(result.Errors.Any(e => e.Contains("requires a scalar value")), Is.True);
        }

        [Test]
        public void NumericRuleStrings_UseInvariantCulture()
        {
            ScenarioDefinition definition = LoadUniversityScenario();
            ScenarioState state = ScenarioState.CreateFromDefinition(definition);
            JObject condition = JObject.Parse("{\"vitals.temp\":{\"lt\":\"37.1\"}}");
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("el-GR");
                Assert.That(new RuleEvaluator().EvaluateCondition(condition, state), Is.True);
                definition.Rules.GlobalRules[0].Condition = condition;
                Assert.That(ScenarioValidator.Validate(definition).IsValid, Is.True);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        private ScenarioDefinition LoadUniversityScenario()
        {
            ScenarioLoadResult result = loader.LoadFromFile(samplePath);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Definition;
        }

    }
}
