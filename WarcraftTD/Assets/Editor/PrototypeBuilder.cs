using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WarcraftTD.Editor
{
    public static class PrototypeBuilder
    {
        [MenuItem("Warcraft TD/Build Android Prototype")]
        public static void BuildAndroid()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in Build Settings.");

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "WarcraftTD-Prototype.apk");

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build failed: {report.summary.result}");

            Debug.Log($"WARCRAFT_TD_BUILD_OK: {outputPath} ({report.summary.totalSize} bytes)");
        }

        public static void BuildWindowsSmokePlayer()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            string outputDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Builds", "Windows"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "WarcraftTD-Prototype.exe");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");

            Debug.Log($"WARCRAFT_TD_WINDOWS_BUILD_OK: {outputPath}");
        }
    }
}
