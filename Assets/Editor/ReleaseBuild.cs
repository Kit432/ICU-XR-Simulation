using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Reproducible Windows release build; never creates scene or UI content.</summary>
public static class ReleaseBuild
{
    public const string ScenePath = "Assets/Scenes/ICUTraining.unity";

    [MenuItem("ICU/Build Windows Release")]
    public static void Windows()
    {
        Directory.CreateDirectory("output/ICU_Submission/Application");
        Directory.CreateDirectory("Evidence");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "ICUSimulation";
        PlayerSettings.productName = "ICU XR Simulation";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "output/ICU_Submission/Application/ICU-XR-Simulation.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        var s = report.summary;
        File.WriteAllText("Evidence/WindowsBuild.txt",
            $"Unity {Application.unityVersion}\nResult: {s.result}\nErrors: {s.totalErrors}\nWarnings: {s.totalWarnings}\n" +
            $"Size bytes: {s.totalSize}\nDuration: {s.totalTime}\nBuilt UTC: {DateTime.UtcNow:O}\n" +
            $"Scene: {ScenePath}\nOutput: {s.outputPath}\nDevelopment build: false\n");
        if (s.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed. See Evidence/WindowsBuild.txt and Unity build log.");
    }
}
