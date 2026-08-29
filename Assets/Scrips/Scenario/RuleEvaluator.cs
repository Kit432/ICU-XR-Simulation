using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ICUSimulation.Scenarios
{
    public sealed class RuleEvaluator
    {
        public event Action<string> WarningRaised;

        public bool EvaluateCondition(JObject condition, ScenarioState state)
        {
            if (condition == null || state == null)
            {
                WarningRaised?.Invoke("A rule condition and ScenarioState are required for evaluation.");
                return false;
            }

            foreach (JProperty pathCondition in condition.Properties())
            {
                if (!TryResolvePath(state, pathCondition.Name, out JToken currentValue))
                {
                    WarningRaised?.Invoke($"Rule condition uses unsupported state path '{pathCondition.Name}'.");
                    return false;
                }

                if (!(pathCondition.Value is JObject comparisons))
                {
                    if (!Compare(currentValue, "eq", pathCondition.Value))
                    {
                        return false;
                    }

                    continue;
                }

                foreach (JProperty comparison in comparisons.Properties())
                {
                    if (!Compare(currentValue, comparison.Name, comparison.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool EvaluateGlobalRule(GlobalRuleDefinition rule, ScenarioState state)
        {
            if (rule == null)
            {
                WarningRaised?.Invoke("Cannot evaluate a null global rule.");
                return false;
            }

            return EvaluateCondition(rule.Condition, state);
        }

        public GateEvaluationResult EvaluateGateRequirements(
            GateRequirements requirements,
            Func<string, string, bool> isFormFieldComplete)
        {
            List<string> missing = new List<string>();

            if (requirements == null)
            {
                return new GateEvaluationResult(true, missing);
            }

            if (requirements.RequiredForms == null || requirements.RequiredForms.Count == 0)
            {
                return new GateEvaluationResult(true, missing);
            }

            foreach (RequiredForm form in requirements.RequiredForms)
            {
                if (form == null || string.IsNullOrWhiteSpace(form.FormId))
                {
                    missing.Add("unknown form");
                    continue;
                }

                if (form.Fields == null || form.Fields.Count == 0)
                {
                    bool formComplete = isFormFieldComplete != null && isFormFieldComplete(form.FormId, null);
                    if (!formComplete)
                    {
                        missing.Add(form.FormId);
                    }

                    continue;
                }

                foreach (string field in form.Fields)
                {
                    bool fieldComplete = isFormFieldComplete != null && isFormFieldComplete(form.FormId, field);
                    if (!fieldComplete)
                    {
                        missing.Add($"{form.FormId}.{field}");
                    }
                }
            }

            return new GateEvaluationResult(missing.Count == 0, missing);
        }

        public bool TryResolvePath(ScenarioState state, string path, out JToken value)
        {
            value = null;
            if (state == null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.StartsWith("flags.", StringComparison.Ordinal))
            {
                string flagName = path.Substring("flags.".Length);
                if (state.Flags != null && state.Flags.TryGetValue(flagName, out bool flagValue))
                {
                    value = new JValue(flagValue);
                    return true;
                }

                return false;
            }

            switch (path)
            {
                case "vitals.hr":
                    value = new JValue(state.HeartRate);
                    return true;
                case "vitals.spo2":
                    value = new JValue(state.OxygenSaturation);
                    return true;
                case "vitals.rr":
                    value = new JValue(state.RespiratoryRate);
                    return true;
                case "vitals.bp":
                    value = new JValue(state.BloodPressure);
                    return true;
                case "vitals.temp":
                    value = new JValue(state.Temperature);
                    return true;
                case "ui.monitor_alert":
                    value = new JValue(state.MonitorAlert);
                    return true;
                case "score":
                case "current_score":
                    value = new JValue(state.CurrentScore);
                    return true;
                case "time_elapsed":
                    value = new JValue(state.ElapsedTime);
                    return true;
                default:
                    return false;
            }
        }

        private bool Compare(JToken currentValue, string comparisonOperator, JToken expectedValue)
        {
            string normalizedOperator = comparisonOperator?.Trim().ToLowerInvariant();

            if (normalizedOperator == "eq" || normalizedOperator == "neq")
            {
                bool equal = ValuesEqual(currentValue, expectedValue);
                return normalizedOperator == "eq" ? equal : !equal;
            }

            if (!TryGetNumber(currentValue, out double currentNumber) ||
                !TryGetNumber(expectedValue, out double expectedNumber))
            {
                WarningRaised?.Invoke(
                    $"Comparison '{comparisonOperator}' requires numeric values, but received '{currentValue}' and '{expectedValue}'.");
                return false;
            }

            switch (normalizedOperator)
            {
                case "lt": return currentNumber < expectedNumber;
                case "lte": return currentNumber <= expectedNumber;
                case "gt": return currentNumber > expectedNumber;
                case "gte": return currentNumber >= expectedNumber;
                default:
                    WarningRaised?.Invoke($"Unsupported comparison operator '{comparisonOperator}'.");
                    return false;
            }
        }

        private static bool ValuesEqual(JToken currentValue, JToken expectedValue)
        {
            if (TryGetNumber(currentValue, out double currentNumber) &&
                TryGetNumber(expectedValue, out double expectedNumber))
            {
                return Math.Abs(currentNumber - expectedNumber) < 0.000001d;
            }

            if (currentValue?.Type == JTokenType.Boolean || expectedValue?.Type == JTokenType.Boolean)
            {
                return currentValue?.Value<bool>() == expectedValue?.Value<bool>();
            }

            return string.Equals(
                currentValue?.Value<string>(),
                expectedValue?.Value<string>(),
                StringComparison.Ordinal);
        }

        private static bool TryGetNumber(JToken token, out double value)
        {
            value = 0d;
            if (token == null)
            {
                return false;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                value = token.Value<double>();
                return true;
            }

            return double.TryParse(token.Value<string>(), out value);
        }
    }
}
