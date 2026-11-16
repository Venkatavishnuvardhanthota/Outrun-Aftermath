using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OutrunAftermath.Client.Multiplayer;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.Abilities;
using OutrunAftermath.Client.Runtime.Objectives;
using OutrunAftermath.Client.Runtime.Vehicles;
using OutrunAftermath.Client.Runtime.Backend;
using OutrunAftermath.Client.Runtime.Audio;
using OutrunAftermath.Client.Runtime.Environment;

namespace OutrunAftermath.Client.Editor
{
    public static class BuildScript
    {
        private const string PlayerPrefabDir = "Assets/Game/Multiplayer/Prefabs";
        private const string PlayerPrefabPath = "Assets/Game/Multiplayer/Prefabs/NetworkPlayer.prefab";
        private const string VehiclePrefabPath = "Assets/Game/Multiplayer/Prefabs/WastelandRaptor.prefab";
        private const string DefaultPrefabsAssetPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        public static void BuildWindows()
        {
            Debug.Log("[BuildScript] Building Phase 8 Polish, Cross-Platform Controls & Optimization Prefabs & Scene...");

            // 1. Create / update NetworkPlayer and WastelandRaptor prefabs
            GameObject playerPrefab = CreateOrUpdateNetworkPlayerPrefab();
            GameObject vehiclePrefab = CreateOrUpdateVehiclePrefab();

            // 2. Register in DefaultNetworkPrefabs list
            NetworkPrefabsList defaultPrefabsList = EnsureDefaultNetworkPrefabs(playerPrefab, vehiclePrefab);

            // 3. Create clean Main.unity scene with NGO stack
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // GameBootstrap
            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            // GameApiClient (Persistent backend API service)
            var apiGo = new GameObject("GameApiClient");
            apiGo.AddComponent<GameApiClient>();

            // NetworkManager + UnityTransport + NetworkSessionController
            var netManagerGo = new GameObject("NetworkManager");
            var netManager = netManagerGo.AddComponent<NetworkManager>();
            var transport = netManagerGo.AddComponent<UnityTransport>();
            netManagerGo.AddComponent<NetworkSessionController>();

            netManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = null, // Spawned explicitly by NetworkMatchManager for Rogue/Sentinel team placement
                EnableSceneManagement = true,
                ForceSamePrefabs = true
            };

            if (!netManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(defaultPrefabsList))
            {
                netManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(defaultPrefabsList);
            }
            EditorUtility.SetDirty(netManager);

            // NetworkMatchManager (scene-placed NetworkObject)
            var matchManagerGo = new GameObject("NetworkMatchManager");
            matchManagerGo.AddComponent<NetworkObject>();
            var matchManager = matchManagerGo.AddComponent<NetworkMatchManager>();
            matchManager.SetPlayerPrefab(playerPrefab);
            matchManager.SetVehiclePrefab(vehiclePrefab);
            EditorUtility.SetDirty(matchManager);

            // NetworkObjectiveTerminal (scene-placed NetworkObject for Ground Operations)
            var terminalGo = new GameObject("NetworkObjectiveTerminal");
            terminalGo.AddComponent<NetworkObject>();
            var terminal = terminalGo.AddComponent<NetworkObjectiveTerminal>();
            EditorUtility.SetDirty(terminal);

            // Phase 7: AudioManager, AtmosphereDirector & MapEnvironmentDirector
            var audioGo = new GameObject("AudioManager");
            audioGo.AddComponent<Runtime.Audio.AudioManager>();

            var atmoGo = new GameObject("AtmosphereDirector");
            atmoGo.AddComponent<Runtime.Environment.AtmosphereDirector>();

            var mapGo = new GameObject("MapEnvironmentDirector");
            mapGo.AddComponent<Runtime.Environment.MapEnvironmentDirector>();

            // Phase 8: SettingsDirector, VirtualTouchControls, PerformanceProfilerHUD & SettingsModalUI
            var settingsGo = new GameObject("SettingsDirector");
            settingsGo.AddComponent<Runtime.Settings.SettingsDirector>();

            var touchGo = new GameObject("VirtualTouchControls");
            touchGo.AddComponent<Runtime.Input.VirtualTouchControls>();

            var profilerGo = new GameObject("PerformanceProfilerHUD");
            profilerGo.AddComponent<Runtime.UI.PerformanceProfilerHUD>();

            var modalGo = new GameObject("SettingsModalUI");
            modalGo.AddComponent<Runtime.UI.SettingsModalUI>();

            // Save Main.unity
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MainScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Build Windows Standalone Player
            string outDir = "Builds/Windows";
            Directory.CreateDirectory(outDir);
            string outputPath = Path.Combine(outDir, "OutrunAftermath.exe").Replace('\\', '/');

            Debug.Log($"[BuildScript] Initiating BuildPlayer -> {outputPath}...");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
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

            Debug.Log($"[BuildScript] PHASE 8 BUILD SUCCEEDED! Total size: {summary.totalSize} bytes. Output: {outputPath}");
        }

        private static GameObject CreateOrUpdateNetworkPlayerPrefab()
        {
            Directory.CreateDirectory(PlayerPrefabDir);

            var tempRoot = new GameObject("NetworkPlayer");
            try
            {
                // NetworkPlayerController brings NetworkObject, NetworkTransform, CharacterController, CharacterMotor, PlayerHealth, WeaponSystem via RequireComponent
                tempRoot.AddComponent<NetworkPlayerController>();

                var netTransform = tempRoot.GetComponent<NetworkTransform>();
                if (netTransform != null)
                {
                    netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                    netTransform.SyncPositionX = true;
                    netTransform.SyncPositionY = true;
                    netTransform.SyncPositionZ = true;
                    netTransform.SyncRotAngleY = true;
                    netTransform.Interpolate = true;
                }

                var cc = tempRoot.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.radius = 0.38f;
                    cc.height = 1.85f;
                    cc.center = new Vector3(0f, 0.925f, 0f);
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, PlayerPrefabPath);
                if (savedPrefab == null)
                {
                    throw new System.Exception($"Failed to save NetworkPlayer prefab at {PlayerPrefabPath}");
                }
                EditorUtility.SetDirty(savedPrefab);
                return savedPrefab;
            }
            finally
            {
                Object.DestroyImmediate(tempRoot);
            }
        }

        private static GameObject CreateOrUpdateVehiclePrefab()
        {
            Directory.CreateDirectory(PlayerPrefabDir);

            var tempRoot = new GameObject("WastelandRaptor");
            try
            {
                // NetworkVehicleController brings NetworkObject, NetworkTransform, Rigidbody via RequireComponent
                tempRoot.AddComponent<NetworkVehicleController>();

                var netTransform = tempRoot.GetComponent<NetworkTransform>();
                if (netTransform != null)
                {
                    netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
                    netTransform.SyncPositionX = true;
                    netTransform.SyncPositionY = true;
                    netTransform.SyncPositionZ = true;
                    netTransform.SyncRotAngleY = true;
                    netTransform.Interpolate = true;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, VehiclePrefabPath);
                if (savedPrefab == null)
                {
                    throw new System.Exception($"Failed to save WastelandRaptor prefab at {VehiclePrefabPath}");
                }
                EditorUtility.SetDirty(savedPrefab);
                return savedPrefab;
            }
            finally
            {
                Object.DestroyImmediate(tempRoot);
            }
        }

        private static NetworkPrefabsList EnsureDefaultNetworkPrefabs(GameObject playerPrefab, GameObject vehiclePrefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultPrefabsAssetPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, DefaultPrefabsAssetPath);
            }

            // Clean any null entries or stale entries
            for (int i = list.PrefabList.Count - 1; i >= 0; i--)
            {
                var entry = list.PrefabList[i];
                if (entry == null || entry.Prefab == null ||
                    entry.Prefab == playerPrefab || entry.Prefab.name == "NetworkPlayer" ||
                    entry.Prefab == vehiclePrefab || entry.Prefab.name == "WastelandRaptor")
                {
                    list.Remove(entry);
                }
            }

            list.Add(new NetworkPrefab { Prefab = playerPrefab });
            list.Add(new NetworkPrefab { Prefab = vehiclePrefab });
            EditorUtility.SetDirty(list);
            return list;
        }
    }
}
