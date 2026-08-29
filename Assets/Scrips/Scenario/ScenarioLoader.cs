using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ICUSimulation.Scenarios
{
    public sealed class ScenarioDescriptor
    {
        public string FilePath { get; internal set; }
        public string FileName => Path.GetFileName(FilePath);
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public ScenarioMetadata Metadata { get; internal set; }
        public IReadOnlyList<string> Warnings { get; internal set; }
    }

    public sealed class ScenarioDiscoveryResult
    {
        public List<ScenarioDescriptor> Scenarios { get; } = new List<ScenarioDescriptor>();
        public List<string> Errors { get; } = new List<string>();
    }

    public sealed class ScenarioLoadResult
    {
        public ScenarioDefinition Definition { get; internal set; }
        public ScenarioValidationResult Validation { get; internal set; }
        public string Error { get; internal set; }
        public bool IsSuccess => Definition != null && Validation != null && Validation.IsValid && string.IsNullOrEmpty(Error);
    }

    public sealed class ScenarioLoader
    {
        public string ScenariosDirectory { get; }

        public ScenarioLoader(string scenariosDirectory = null)
        {
            ScenariosDirectory = string.IsNullOrWhiteSpace(scenariosDirectory)
                ? Path.Combine(Application.streamingAssetsPath, "Scenarios")
                : scenariosDirectory;
        }

        public ScenarioDiscoveryResult DiscoverScenarios()
        {
            ScenarioDiscoveryResult discovery = new ScenarioDiscoveryResult();

            try
            {
                if (!Directory.Exists(ScenariosDirectory))
                {
                    discovery.Errors.Add($"Scenario directory was not found: {ScenariosDirectory}");
                    return discovery;
                }

                foreach (string filePath in Directory.EnumerateFiles(ScenariosDirectory, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    ScenarioLoadResult loaded = LoadFromFile(filePath);
                    if (!loaded.IsSuccess)
                    {
                        discovery.Errors.Add($"{Path.GetFileName(filePath)}: {loaded.Error ?? loaded.Validation?.FormatErrors()}");
                        continue;
                    }

                    discovery.Scenarios.Add(new ScenarioDescriptor
                    {
                        FilePath = filePath,
                        Id = loaded.Definition.Metadata.Id,
                        Title = loaded.Definition.Metadata.Title,
                        Metadata = loaded.Definition.Metadata,
                        Warnings = loaded.Validation.Warnings.AsReadOnly()
                    });
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                discovery.Errors.Add($"Could not discover scenarios in '{ScenariosDirectory}': {exception.Message}");
            }

            return discovery;
        }

        public ScenarioLoadResult LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Failure("A scenario file path is required.");
            }

            try
            {
                return LoadFromJson(File.ReadAllText(filePath));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Failure($"Could not read scenario file '{filePath}': {exception.Message}");
            }
        }

        public ScenarioLoadResult LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure("Scenario JSON is empty.");
            }

            try
            {
                ScenarioDefinition definition = JsonConvert.DeserializeObject<ScenarioDefinition>(json);
                ScenarioValidationResult validation = ScenarioValidator.Validate(definition);

                return new ScenarioLoadResult
                {
                    Definition = definition,
                    Validation = validation,
                    Error = validation.IsValid ? null : validation.FormatErrors()
                };
            }
            catch (JsonException exception)
            {
                return Failure($"Malformed scenario JSON: {exception.Message}");
            }
            catch (Exception exception)
            {
                return Failure($"Scenario could not be loaded: {exception.Message}");
            }
        }

        private static ScenarioLoadResult Failure(string error)
        {
            return new ScenarioLoadResult
            {
                Error = error,
                Validation = new ScenarioValidationResult()
            };
        }
    }
}
