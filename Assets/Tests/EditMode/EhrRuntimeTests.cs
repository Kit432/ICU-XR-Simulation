using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICUSimulation.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace ICUSimulation.Tests.EditMode
{
    public sealed class EhrRuntimeTests
    {
        private sealed class GateHarness
        {
            public DocumentationStore Store;
            public ScenarioRunner Runner;
        }

        private ScenarioDefinition definition;

        [SetUp]
        public void SetUp()
        {
            string scenarioPath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Scenarios",
                "icu_scenario_hypoxia_v1.json");
            ScenarioLoadResult loaded = new ScenarioLoader().LoadFromFile(scenarioPath);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            definition = loaded.Definition;
        }

        [Test]
        public void EhrConfiguration_IsReadFromUniversityJson()
        {
            Assert.That(definition.EhrConfiguration, Is.Not.Null);
            Assert.That(definition.EhrConfiguration.Forms, Is.Not.Null);
        }

        [Test]
        public void UniversityForms_AreDiscoveredFromConfiguration()
        {
            Assert.That(definition.EhrConfiguration.Forms.Keys, Is.EquivalentTo(new[]
            {
                "assessment_form",
                "intervention_form",
                "communication_log"
            }));
        }

        [Test]
        public void DocumentationStore_SavesAndReturnsFieldValues()
        {
            DocumentationStore store = NewStore();
            store.SaveForm("assessment_form", new Dictionary<string, string>
            {
                ["observation"] = "  Cyanosis observed  "
            });

            Assert.That(store.HasFormSubmission("assessment_form"), Is.True);
            Assert.That(store.HasFieldValue("assessment_form", "observation"), Is.True);
            Assert.That(store.GetFieldValue("assessment_form", "observation"), Is.EqualTo("Cyanosis observed"));
        }

        [Test]
        public void DocumentationStore_ResetClearsDocumentation()
        {
            DocumentationStore store = NewStore();
            store.SaveForm("assessment_form", Fields("observation", "value"));

            store.ResetForScenario(definition.Metadata.Id);

            Assert.That(store.SubmittedFormCount, Is.Zero);
            Assert.That(store.HasFieldValue("assessment_form", "observation"), Is.False);
        }

        [Test]
        public void FirstGate_RemainsBlockedWithNoDocumentation()
        {
            GateHarness harness = ReachFirstGate();

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n4_gate_documentation_1"));
        }

        [Test]
        public void FirstGate_RemainsBlockedWithOnlyObservation()
        {
            GateHarness harness = ReachFirstGate();
            harness.Store.SaveForm("assessment_form", Fields("observation", "Patient assessed"));

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.MissingRequirements, Does.Contain("intervention_form.fiO2_setting"));
        }

        [Test]
        public void FirstGate_RemainsBlockedWithOnlyFio2Setting()
        {
            GateHarness harness = ReachFirstGate();
            harness.Store.SaveForm("intervention_form", Fields("fiO2_setting", "100%"));

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.MissingRequirements, Does.Contain("assessment_form.observation"));
        }

        [Test]
        public void FirstGate_PassesWhenBothRequiredFieldsExist()
        {
            GateHarness harness = ReachFirstGate();
            SaveFirstGateRequirements(harness.Store);

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.True);
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n5_reassessment"));
        }

        [Test]
        public void FirstGate_PassAppliesDocumentationFlag()
        {
            GateHarness harness = ReachFirstGate();
            SaveFirstGateRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Runner.State.Flags["documentation_1_complete"], Is.True);
        }

        [Test]
        public void FirstGate_PassAppliesScoreDelta()
        {
            GateHarness harness = ReachFirstGate();
            int scoreBefore = harness.Runner.State.CurrentScore;
            SaveFirstGateRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Runner.State.CurrentScore, Is.EqualTo(scoreBefore + 10));
        }

        [Test]
        public void CommunicationGate_RemainsBlockedWithoutFields()
        {
            GateHarness harness = ReachCommunicationGate();

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n7_gate_documentation_2"));
        }

        [Test]
        public void CommunicationGate_RemainsBlockedWithRecipientOnly()
        {
            GateHarness harness = ReachCommunicationGate();
            harness.Store.SaveForm("communication_log", Fields("recipient", "On-call physician"));

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.MissingRequirements, Does.Contain("communication_log.outcome"));
        }

        [Test]
        public void CommunicationGate_RemainsBlockedWithOutcomeOnly()
        {
            GateHarness harness = ReachCommunicationGate();
            harness.Store.SaveForm("communication_log", Fields("outcome", "Physician notified"));

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.MissingRequirements, Does.Contain("communication_log.recipient"));
        }

        [Test]
        public void CommunicationGate_PassesWithRecipientAndOutcome()
        {
            GateHarness harness = ReachCommunicationGate();
            SaveCommunicationRequirements(harness.Store);

            GateEvaluationResult result = harness.Runner.EvaluateCurrentGate();

            Assert.That(result.IsSatisfied, Is.True);
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n8_end_scenario"));
        }

        [Test]
        public void CommunicationGate_PassAppliesDocumentationFlag()
        {
            GateHarness harness = ReachCommunicationGate();
            SaveCommunicationRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Runner.State.Flags["documentation_2_complete"], Is.True);
        }

        [Test]
        public void CommunicationGate_PassAppliesScoreDelta()
        {
            GateHarness harness = ReachCommunicationGate();
            int scoreBefore = harness.Runner.State.CurrentScore;
            SaveCommunicationRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Runner.State.CurrentScore, Is.EqualTo(scoreBefore + 5));
        }

        [Test]
        public void OptionalConfiguredFields_AreNotRequiredForGatePass()
        {
            GateHarness harness = ReachFirstGate();
            SaveFirstGateRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Store.HasFieldValue("assessment_form", "skin_color"), Is.False);
            Assert.That(harness.Store.HasFieldValue("intervention_form", "flow_rate"), Is.False);
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n5_reassessment"));
        }

        [Test]
        public void ScenarioReset_RemovesAllDocumentation()
        {
            DocumentationStore store = NewStore();
            store.SaveForm("assessment_form", Fields("observation", "value"));
            ScenarioSession session = new ScenarioSession();
            session.LoadScenario(definition);

            session.ResetScenario();
            store.ResetForScenario(definition.Metadata.Id);

            Assert.That(store.SubmittedFormCount, Is.Zero);
        }

        [Test]
        public void Documentation_DoesNotLeakIntoAnotherScenarioSession()
        {
            DocumentationStore store = NewStore();
            store.SaveForm("assessment_form", Fields("observation", "value"));

            store.ResetForScenario("another_scenario");

            Assert.That(store.ActiveScenarioId, Is.EqualTo("another_scenario"));
            Assert.That(store.HasFieldValue("assessment_form", "observation"), Is.False);
        }

        [Test]
        public void GatePass_EventOccursOnlyOnce()
        {
            GateHarness harness = ReachFirstGate();
            int passCount = 0;
            harness.Runner.GatePassed += gate => passCount++;
            SaveFirstGateRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();
            harness.Runner.EvaluateCurrentGate();

            Assert.That(passCount, Is.EqualTo(1));
        }

        [Test]
        public void RealDocumentationResolver_CompletesBothGatesWithoutDebugBypass()
        {
            GateHarness harness = ReachFirstGate();
            SaveFirstGateRequirements(harness.Store);
            harness.Runner.EvaluateCurrentGate();
            harness.Runner.ContinueMessage();
            harness.Runner.TryHandleHotspotInteraction("hs_call");
            SaveCommunicationRequirements(harness.Store);

            harness.Runner.EvaluateCurrentGate();

            Assert.That(harness.Runner.IsCompleted, Is.True);
            Assert.That(harness.Runner.State.Flags["documentation_1_complete"], Is.True);
            Assert.That(harness.Runner.State.Flags["documentation_2_complete"], Is.True);
        }

        private GateHarness ReachFirstGate()
        {
            DocumentationStore store = NewStore();
            ScenarioRunner runner = new ScenarioRunner(
                definition,
                ScenarioState.CreateFromDefinition(definition))
            {
                GateRequirementResolver = store.HasFieldValue
            };
            runner.Start();
            runner.ContinueMessage();
            runner.TryHandleHotspotInteraction("hs_patient");
            runner.TryHandleHotspotInteraction("hs_ventilator");
            Assert.That(runner.CurrentNode.Id, Is.EqualTo("n4_gate_documentation_1"));
            return new GateHarness { Store = store, Runner = runner };
        }

        private GateHarness ReachCommunicationGate()
        {
            GateHarness harness = ReachFirstGate();
            SaveFirstGateRequirements(harness.Store);
            harness.Runner.EvaluateCurrentGate();
            harness.Runner.ContinueMessage();
            harness.Runner.TryHandleHotspotInteraction("hs_call");
            Assert.That(harness.Runner.CurrentNode.Id, Is.EqualTo("n7_gate_documentation_2"));
            return harness;
        }

        private DocumentationStore NewStore()
        {
            DocumentationStore store = new DocumentationStore();
            store.ResetForScenario(definition.Metadata.Id);
            return store;
        }

        private static void SaveFirstGateRequirements(DocumentationStore store)
        {
            store.SaveForm("assessment_form", Fields("observation", "Patient assessed"));
            store.SaveForm("intervention_form", Fields("fiO2_setting", "100%"));
        }

        private static void SaveCommunicationRequirements(DocumentationStore store)
        {
            store.SaveForm("communication_log", new Dictionary<string, string>
            {
                ["recipient"] = "On-call physician",
                ["outcome"] = "Physician notified"
            });
        }

        private static Dictionary<string, string> Fields(string fieldId, string value)
        {
            return new Dictionary<string, string> { [fieldId] = value };
        }
    }
}
