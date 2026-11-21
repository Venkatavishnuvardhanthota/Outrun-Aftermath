using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Character;

namespace OutrunAftermath.Client.Editor
{
    public static class CharacterPlayModeAcceptance
    {
        private const string ScenePath = "Assets/Scenes/CharacterVisualAcceptance.unity";
        private const string AcceptanceExeRelative = "Builds/AcceptancePlayer/OutrunAftermath_Acceptance.exe";

        [MenuItem("Outrun/Run Phase C.1.3.6 Play Mode Acceptance (In Editor)")]
        public static void RunPlayModeMenu()
        {
            SetupSceneAndEnterPlayMode();
        }

        [MenuItem("Outrun/Build Acceptance Standalone Player")]
        public static void BuildAcceptancePlayerMenu()
        {
            BuildAcceptancePlayer();
        }

        public static void BuildAcceptancePlayerBatchMode()
        {
            try
            {
                Debug.Log("[CharacterPlayModeAcceptance C.1.3.6] Initiating Standalone Acceptance Player Build...");
                BuildAcceptancePlayer();
                Debug.Log("[CharacterPlayModeAcceptance C.1.3.6] Acceptance Player Build Succeeded!");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterPlayModeAcceptance C.1.3.6] Build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void SetupSceneAndEnterPlayMode()
        {
            EnsureDriverInScene();

            string sentinelPath = Path.Combine(Application.dataPath, "..", "playmode_acceptance_done.txt");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            Debug.Log("[CharacterPlayModeAcceptance C.1.3.6] Entering Play Mode now...");
            EditorApplication.EnterPlaymode();
        }

        public static void EnsureDriverInScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var driverGo = GameObject.Find("AcceptanceDriver");
            if (driverGo == null)
            {
                driverGo = new GameObject("AcceptanceDriver");
            }

            var driver = driverGo.GetComponent<CharacterPlayModeAcceptanceDriver>();
            if (driver == null)
            {
                driver = driverGo.AddComponent<CharacterPlayModeAcceptanceDriver>();
            }

            // Remove any legacy static operator instances so driver spawns clean fresh instances in Play Mode
            var existingRex = GameObject.Find("Operator_REX");
            if (existingRex != null) UnityEngine.Object.DestroyImmediate(existingRex);

            var existingTrace = GameObject.Find("Operator_TRACE");
            if (existingTrace != null) UnityEngine.Object.DestroyImmediate(existingTrace);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildAcceptancePlayer()
        {
            EnsureDriverInScene();

            string outDir = Path.GetDirectoryName(AcceptanceExeRelative);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = AcceptanceExeRelative.Replace('\\', '/'),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Acceptance Player build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"[CharacterPlayModeAcceptance C.1.3.6] Standalone Acceptance Player built successfully: {report.summary.totalSize} bytes");
        }
    }
}
