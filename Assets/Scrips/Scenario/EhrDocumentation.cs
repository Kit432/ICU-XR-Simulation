using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ICUSimulation.Scenarios
{
    public sealed class DocumentationStore
    {
        private readonly Dictionary<string, Dictionary<string, string>> valuesByForm =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private readonly HashSet<string> submittedForms = new HashSet<string>(StringComparer.Ordinal);

        public int SubmittedFormCount => submittedForms.Count;
        public string ActiveScenarioId { get; private set; }

        public void ResetForScenario(string scenarioId)
        {
            Clear();
            ActiveScenarioId = scenarioId;
        }

        public void SaveForm(string formId, IReadOnlyDictionary<string, string> fieldValues)
        {
            if (string.IsNullOrWhiteSpace(formId))
            {
                throw new ArgumentException("A form ID is required.", nameof(formId));
            }

            Dictionary<string, string> storedFields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (fieldValues != null)
            {
                foreach (KeyValuePair<string, string> field in fieldValues)
                {
                    if (string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Value))
                    {
                        continue;
                    }

                    storedFields[field.Key] = field.Value.Trim();
                }
            }

            valuesByForm[formId] = storedFields;
            submittedForms.Add(formId);
        }

        public bool HasFormSubmission(string formId)
        {
            return !string.IsNullOrWhiteSpace(formId) && submittedForms.Contains(formId);
        }

        public bool HasFieldValue(string formId, string fieldId)
        {
            if (string.IsNullOrWhiteSpace(formId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(fieldId))
            {
                return HasFormSubmission(formId);
            }

            return valuesByForm.TryGetValue(formId, out Dictionary<string, string> fields) &&
                   fields.TryGetValue(fieldId, out string value) &&
                   !string.IsNullOrWhiteSpace(value);
        }

        public string GetFieldValue(string formId, string fieldId)
        {
            return valuesByForm.TryGetValue(formId ?? string.Empty, out Dictionary<string, string> fields) &&
                   fields.TryGetValue(fieldId ?? string.Empty, out string value)
                ? value
                : null;
        }

        public IReadOnlyDictionary<string, string> GetFormValues(string formId)
        {
            return valuesByForm.TryGetValue(formId ?? string.Empty, out Dictionary<string, string> fields)
                ? new Dictionary<string, string>(fields, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public void Clear()
        {
            valuesByForm.Clear();
            submittedForms.Clear();
        }
    }

    public sealed class EhrValidationResult
    {
        public bool IsValid => MissingFieldIds.Count == 0 && string.IsNullOrEmpty(ErrorMessage);
        public IReadOnlyList<string> MissingFieldIds { get; }
        public string ErrorMessage { get; }

        public EhrValidationResult(IReadOnlyList<string> missingFieldIds, string errorMessage = null)
        {
            MissingFieldIds = missingFieldIds ?? Array.Empty<string>();
            ErrorMessage = errorMessage;
        }
    }

    public static class EhrDocumentationValidator
    {
        public static EhrValidationResult Validate(
            IEnumerable<string> requiredFieldIds,
            IReadOnlyDictionary<string, string> fieldValues)
        {
            List<string> missing = new List<string>();
            if (requiredFieldIds == null)
            {
                return new EhrValidationResult(missing);
            }

            foreach (string fieldId in requiredFieldIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                if (fieldValues == null ||
                    !fieldValues.TryGetValue(fieldId, out string value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    missing.Add(fieldId);
                }
            }

            return new EhrValidationResult(missing);
        }
    }

    public static class EhrDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> KnownFieldLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["observation"] = "Observation / Παρατήρηση",
                ["skin_color"] = "Skin colour",
                ["consciousness"] = "Consciousness",
                ["device"] = "Device",
                ["fiO2_setting"] = "FiO2 setting",
                ["flow_rate"] = "Flow rate",
                ["recipient"] = "Recipient",
                ["reason"] = "Reason",
                ["outcome"] = "Outcome"
            };

        public static string GetFieldLabel(string fieldId)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                return "Unknown field";
            }

            if (KnownFieldLabels.TryGetValue(fieldId, out string knownLabel))
            {
                return knownLabel;
            }

            string[] words = fieldId.Replace('-', '_').Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return fieldId;
            }

            StringBuilder result = new StringBuilder();
            foreach (string word in words)
            {
                if (result.Length > 0)
                {
                    result.Append(' ');
                }

                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    result.Append(word.Substring(1));
                }
            }

            return result.ToString();
        }

        public static string GetFormTitle(ScenarioDefinition definition, string formId)
        {
            if (definition?.EhrConfiguration?.Forms != null &&
                !string.IsNullOrWhiteSpace(formId) &&
                definition.EhrConfiguration.Forms.TryGetValue(formId, out EhrFormDefinition form) &&
                !string.IsNullOrWhiteSpace(form?.Title))
            {
                return form.Title;
            }

            return GetFieldLabel(formId);
        }
    }

    public sealed class EhrFormSubmittedEvent
    {
        public string FormId { get; }
        public IReadOnlyDictionary<string, string> FieldValues { get; }
        public float RuntimeTime { get; }

        public EhrFormSubmittedEvent(
            string formId,
            IReadOnlyDictionary<string, string> fieldValues,
            float runtimeTime)
        {
            FormId = formId;
            FieldValues = fieldValues != null
                ? new Dictionary<string, string>(fieldValues, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            RuntimeTime = runtimeTime;
        }
    }

    public sealed class DocumentationRequirementSatisfiedEvent
    {
        public string GateNodeId { get; }
        public float RuntimeTime { get; }

        public DocumentationRequirementSatisfiedEvent(string gateNodeId, float runtimeTime)
        {
            GateNodeId = gateNodeId;
            RuntimeTime = runtimeTime;
        }
    }
}
