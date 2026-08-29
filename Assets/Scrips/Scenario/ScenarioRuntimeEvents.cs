using System;
using System.Collections.Generic;

namespace ICUSimulation.Scenarios
{
    public enum ScenarioFeedbackStyle
    {
        Info,
        Success,
        Warning,
        Danger
    }

    public sealed class ScenarioFeedbackRequest
    {
        public string Message { get; }
        public ScenarioFeedbackStyle Style { get; }
        public string SourceId { get; }

        public ScenarioFeedbackRequest(string message, ScenarioFeedbackStyle style, string sourceId = null)
        {
            Message = message;
            Style = style;
            SourceId = sourceId;
        }
    }

    public sealed class ScenarioUiEffectRequest
    {
        public string EffectType { get; }
        public string Target { get; }
        public string State { get; }
        public string SourceId { get; }
        public bool IsActive { get; }

        public ScenarioUiEffectRequest(
            string effectType,
            string target,
            string state,
            string sourceId = null,
            bool isActive = true)
        {
            EffectType = effectType;
            Target = target;
            State = state;
            SourceId = sourceId;
            IsActive = isActive;
        }
    }

    public sealed class ScenarioScoreChangedEvent
    {
        public int PreviousScore { get; }
        public int CurrentScore { get; }
        public int Delta { get; }

        public ScenarioScoreChangedEvent(int previousScore, int currentScore)
        {
            PreviousScore = previousScore;
            CurrentScore = currentScore;
            Delta = currentScore - previousScore;
        }
    }

    public sealed class ScenarioVitalsChangedEvent
    {
        public string VitalName { get; }
        public object PreviousValue { get; }
        public object CurrentValue { get; }

        public ScenarioVitalsChangedEvent(string vitalName, object previousValue, object currentValue)
        {
            VitalName = vitalName;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    public sealed class ScenarioFlagChangedEvent
    {
        public string FlagName { get; }
        public bool PreviousValue { get; }
        public bool CurrentValue { get; }

        public ScenarioFlagChangedEvent(string flagName, bool previousValue, bool currentValue)
        {
            FlagName = flagName;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    public sealed class ScenarioOptionSelectedEvent
    {
        public ScenarioNode Node { get; }
        public ScenarioOption Option { get; }
        public string HotspotId { get; }

        public ScenarioOptionSelectedEvent(ScenarioNode node, ScenarioOption option, string hotspotId)
        {
            Node = node;
            Option = option;
            HotspotId = hotspotId;
        }
    }

    public sealed class ScenarioTimeoutEvent
    {
        public ScenarioNode Node { get; }
        public float ConfiguredSeconds { get; }

        public ScenarioTimeoutEvent(ScenarioNode node, float configuredSeconds)
        {
            Node = node;
            ConfiguredSeconds = configuredSeconds;
        }
    }

    public sealed class ScenarioGlobalRuleEvent
    {
        public GlobalRuleDefinition Rule { get; }

        public ScenarioGlobalRuleEvent(GlobalRuleDefinition rule)
        {
            Rule = rule;
        }
    }

    public sealed class ScenarioGateEvent
    {
        public ScenarioNode Node { get; }
        public GateEvaluationResult Evaluation { get; }
        public bool WasDebugBypassed { get; }

        public ScenarioGateEvent(ScenarioNode node, GateEvaluationResult evaluation, bool wasDebugBypassed)
        {
            Node = node;
            Evaluation = evaluation;
            WasDebugBypassed = wasDebugBypassed;
        }
    }

    public sealed class GateEvaluationResult
    {
        public bool IsSatisfied { get; }
        public IReadOnlyList<string> MissingRequirements { get; }

        public GateEvaluationResult(bool isSatisfied, IReadOnlyList<string> missingRequirements)
        {
            IsSatisfied = isSatisfied;
            MissingRequirements = missingRequirements ?? Array.Empty<string>();
        }
    }
}
