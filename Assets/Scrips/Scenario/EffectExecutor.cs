using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ICUSimulation.Scenarios
{
    public sealed class EffectExecutor
    {
        public event Action<ScenarioState> StateChanged;
        public event Action<ScenarioScoreChangedEvent> ScoreChanged;
        public event Action<ScenarioVitalsChangedEvent> VitalsChanged;
        public event Action<ScenarioFlagChangedEvent> FlagChanged;
        public event Action<ScenarioFeedbackRequest> FeedbackRequested;
        public event Action<ScenarioUiEffectRequest> UiEffectRequested;
        public event Action<string> WarningRaised;

        public bool ApplyBundle(
            ScenarioEffectBundle effects,
            ScenarioState state,
            string sourceId = null,
            ScenarioFeedbackStyle? feedbackStyleOverride = null)
        {
            if (effects == null || state == null)
            {
                return false;
            }

            bool changed = false;

            if (effects.ScoreDelta.HasValue && effects.ScoreDelta.Value != 0)
            {
                int previousScore = state.CurrentScore;
                state.CurrentScore += effects.ScoreDelta.Value;
                changed = true;
                ScoreChanged?.Invoke(new ScenarioScoreChangedEvent(previousScore, state.CurrentScore));
            }

            if (effects.StateUpdate != null)
            {
                foreach (KeyValuePair<string, JToken> update in effects.StateUpdate)
                {
                    changed |= ApplyPathUpdate(state, update.Key, update.Value);
                }
            }

            if (effects.VitalsUpdate != null)
            {
                foreach (KeyValuePair<string, JToken> update in effects.VitalsUpdate)
                {
                    string vitalPath = update.Key.StartsWith("vitals.", StringComparison.Ordinal)
                        ? update.Key
                        : "vitals." + update.Key;
                    changed |= ApplyPathUpdate(state, vitalPath, update.Value);
                }
            }

            if (changed)
            {
                StateChanged?.Invoke(state);
            }

            if (!string.IsNullOrWhiteSpace(effects.Toast))
            {
                ScenarioFeedbackStyle style = feedbackStyleOverride ?? InferFeedbackStyle(effects.ScoreDelta);
                FeedbackRequested?.Invoke(new ScenarioFeedbackRequest(effects.Toast, style, sourceId));
            }

            return changed;
        }

        public void ApplyEffects(
            IEnumerable<ScenarioEffect> effects,
            ScenarioState state,
            string sourceId = null,
            bool isActive = true)
        {
            if (effects == null)
            {
                return;
            }

            foreach (ScenarioEffect effect in effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Type))
                {
                    WarningRaised?.Invoke($"Effect from '{sourceId ?? "unknown source"}' has no type.");
                    continue;
                }

                switch (effect.Type.Trim().ToLowerInvariant())
                {
                    case "ui_toast":
                        if (isActive && !string.IsNullOrWhiteSpace(effect.Message))
                        {
                            FeedbackRequested?.Invoke(new ScenarioFeedbackRequest(
                                effect.Message,
                                ParseFeedbackStyle(effect.Style),
                                sourceId));
                        }
                        break;

                    case "ui_visual":
                        UiEffectRequested?.Invoke(new ScenarioUiEffectRequest(
                            effect.Type,
                            effect.Target,
                            effect.State,
                            sourceId,
                            isActive));
                        break;

                    default:
                        WarningRaised?.Invoke(
                            $"Unsupported scenario effect type '{effect.Type}' from '{sourceId ?? "unknown source"}'.");
                        break;
                }
            }
        }

        public void RequestFeedback(
            string message,
            ScenarioFeedbackStyle style,
            string sourceId = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                FeedbackRequested?.Invoke(new ScenarioFeedbackRequest(message, style, sourceId));
            }
        }

        private bool ApplyPathUpdate(ScenarioState state, string path, JToken value)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                WarningRaised?.Invoke("An effect contained an empty state-update path.");
                return false;
            }

            if (path.StartsWith("flags.", StringComparison.Ordinal))
            {
                return ApplyFlag(state, path.Substring("flags.".Length), value);
            }

            try
            {
                switch (path)
                {
                    case "vitals.hr":
                        return SetVital("hr", state.HeartRate, value.Value<int>(), next => state.HeartRate = next);
                    case "vitals.spo2":
                        return SetVital("spo2", state.OxygenSaturation, value.Value<int>(), next => state.OxygenSaturation = next);
                    case "vitals.rr":
                        return SetVital("rr", state.RespiratoryRate, value.Value<int>(), next => state.RespiratoryRate = next);
                    case "vitals.bp":
                        return SetVital("bp", state.BloodPressure, value.Value<string>(), next => state.BloodPressure = next);
                    case "vitals.temp":
                        return SetVital("temp", state.Temperature, value.Value<float>(), next => state.Temperature = next);
                    case "ui.monitor_alert":
                    {
                        bool next = value.Value<bool>();
                        if (state.MonitorAlert == next)
                        {
                            return false;
                        }

                        state.MonitorAlert = next;
                        return true;
                    }
                    case "ui.active_hotspots":
                    {
                        if (!(value is JArray hotspotArray))
                        {
                            WarningRaised?.Invoke("Path 'ui.active_hotspots' requires a JSON array.");
                            return false;
                        }

                        List<string> next = hotspotArray.Values<string>().ToList();
                        if (state.ActiveHotspots != null && state.ActiveHotspots.SequenceEqual(next))
                        {
                            return false;
                        }

                        state.ActiveHotspots = next;
                        return true;
                    }
                    default:
                        WarningRaised?.Invoke($"Unsupported state-update path '{path}'.");
                        return false;
                }
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is InvalidCastException ||
                exception is ArgumentException ||
                exception is OverflowException)
            {
                WarningRaised?.Invoke($"Could not apply value '{value}' to path '{path}': {exception.Message}");
                return false;
            }
        }

        private bool ApplyFlag(ScenarioState state, string flagName, JToken value)
        {
            if (string.IsNullOrWhiteSpace(flagName))
            {
                WarningRaised?.Invoke("A flag update must include a flag name after 'flags.'.");
                return false;
            }

            bool next;
            try
            {
                next = value.Value<bool>();
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is InvalidCastException ||
                exception is ArgumentException ||
                exception is OverflowException)
            {
                WarningRaised?.Invoke($"Flag '{flagName}' requires a boolean value: {exception.Message}");
                return false;
            }

            bool previous = state.Flags != null && state.Flags.TryGetValue(flagName, out bool current) && current;
            if (state.Flags == null)
            {
                state.Flags = new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            if (state.Flags.ContainsKey(flagName) && previous == next)
            {
                return false;
            }

            state.Flags[flagName] = next;
            FlagChanged?.Invoke(new ScenarioFlagChangedEvent(flagName, previous, next));
            return true;
        }

        private bool SetVital<T>(string name, T previous, T next, Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(previous, next))
            {
                return false;
            }

            setter(next);
            VitalsChanged?.Invoke(new ScenarioVitalsChangedEvent(name, previous, next));
            return true;
        }

        private static ScenarioFeedbackStyle InferFeedbackStyle(int? scoreDelta)
        {
            if (!scoreDelta.HasValue || scoreDelta.Value == 0)
            {
                return ScenarioFeedbackStyle.Info;
            }

            return scoreDelta.Value > 0
                ? ScenarioFeedbackStyle.Success
                : ScenarioFeedbackStyle.Danger;
        }

        private static ScenarioFeedbackStyle ParseFeedbackStyle(string style)
        {
            switch (style?.Trim().ToLowerInvariant())
            {
                case "success": return ScenarioFeedbackStyle.Success;
                case "warning": return ScenarioFeedbackStyle.Warning;
                case "danger": return ScenarioFeedbackStyle.Danger;
                default: return ScenarioFeedbackStyle.Info;
            }
        }
    }
}
