using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;
using OutrunAftermath.Client;

namespace OutrunAftermath.Client.Editor
{
    public static class BuildScript
    {
        public static void BuildWindows()
        {
            Debug.Log("[BuildScript] Creating Phase 1 Core Scene: Assets/Scenes/Main.unity...");

            // Create a clean scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Add the GameBootstrap which initializes Dead City, player, camera, weapons, HUD
            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            // Save the scene
            Directory.CreateDirectory("Assets/Scenes");
            string scenePath = "Assets/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Build Windows Standalone Player
            string outDir = "Builds/Windows";
            Directory.CreateDirectory(outDir);
            string outputPath = Path.Combine(outDir, "OutrunAftermath.exe").Replace('\\', '/');

            Debug.Log($"[BuildScript] Initiating BuildPlayer -> {outputPath}...");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Build failed: {summary.result} ({summary.totalErrors} errors)");
            }

            Debug.Log($"[BuildScript] PHASE 1 BUILD SUCCEEDED! Total size: {summary.totalSize} bytes. Output: {outputPath}");
        }
    }
}
