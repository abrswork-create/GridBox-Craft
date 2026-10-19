using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Codemagic CI build entrypoints for CraftGridBox.
/// Called via -executeMethod BuildScript.BuildAndroidAAB and BuildScript.BuildIOS
/// </summary>
public static class BuildScript
{
    // ─── Shared helpers ──────────────────────────────────────────────────────

    /// <summary>Returns all scenes listed in EditorBuildSettings.</summary>
    private static string[] GetScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var paths = new System.Collections.Generic.List<string>();
        foreach (var s in scenes)
            if (s.enabled) paths.Add(s.path);
        return paths.ToArray();
    }

    /// <summary>Read a command-line argument value, e.g. -keystorePass foo.</summary>
    private static string GetArg(string name, string fallback = "")
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    private static void FailBuild(string message)
    {
        Debug.LogError("[BuildScript] " + message);
        EditorApplication.Exit(1);
    }

    // ─── Android AAB ─────────────────────────────────────────────────────────

    public static void BuildAndroidAAB()
    {
        Debug.Log("[BuildScript] Starting Android AAB build...");

        // Output path
        string outputDir  = GetArg("-customBuildPath", "build/Android");
        string outputPath = Path.Combine(outputDir, "CraftGridBox.aab");
        Directory.CreateDirectory(outputDir);

        // Keystore (passed via codemagic.yaml command-line args)
        string keystoreName  = GetArg("-keystoreName");
        string keystorePass  = GetArg("-keystorePass");
        string keyaliasName  = GetArg("-keyaliasName",  PlayerSettings.Android.keyaliasName);
        string keyaliasPass  = GetArg("-keyaliasPass");

        if (!string.IsNullOrEmpty(keystoreName))
        {
            PlayerSettings.Android.useCustomKeystore   = true;
            PlayerSettings.Android.keystoreName        = keystoreName;
            PlayerSettings.Android.keystorePass        = keystorePass;
            PlayerSettings.Android.keyaliasName        = keyaliasName;
            PlayerSettings.Android.keyaliasPass        = keyaliasPass;
            Debug.Log("[BuildScript] Keystore configured: " + keystoreName);
        }
        else
        {
            Debug.LogWarning("[BuildScript] No -keystoreName supplied; using project keystore settings.");
        }

        // Force AAB
        EditorUserBuildSettings.buildAppBundle = true;

        var options = new BuildPlayerOptions
        {
            scenes      = GetScenes(),
            locationPathName = outputPath,
            target      = BuildTarget.Android,
            options     = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[BuildScript] Android AAB build succeeded → " + outputPath);
            EditorApplication.Exit(0);
        }
        else
        {
            FailBuild("Android AAB build FAILED. Result: " + report.summary.result);
        }
    }

    // ─── iOS Xcode project ───────────────────────────────────────────────────

    public static void BuildIOS()
    {
        Debug.Log("[BuildScript] Starting iOS Xcode project build...");

        string outputDir = GetArg("-customBuildPath", "build/iOS");
        Directory.CreateDirectory(outputDir);

        // Automatically sign if team ID env var is present
        string teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID") ?? "";
        if (!string.IsNullOrEmpty(teamId))
        {
            PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.iOS,
                GetArg("-bundleId", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS))
            );
        }

        var options = new BuildPlayerOptions
        {
            scenes           = GetScenes(),
            locationPathName = outputDir,
            target           = BuildTarget.iOS,
            options          = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[BuildScript] iOS Xcode project build succeeded → " + outputDir);
            EditorApplication.Exit(0);
        }
        else
        {
            FailBuild("iOS build FAILED. Result: " + report.summary.result);
        }
    }
}
