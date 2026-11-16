using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
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
            SetupPrefabsAndScene();
            BuildStandalone("Builds/Windows/OutrunAftermath.exe", false);
        }

        public static void BuildWindowsClient()
        {
            SetupPrefabsAndScene();
            BuildStandalone("Builds/WindowsClient/OutrunAftermath_Client.exe", false);
        }

        public static void BuildWindowsServer()
        {
            SetupPrefabsAndScene();
            BuildStandalone("Builds/WindowsServer/OutrunAftermath_Server.exe", true);
        }

        public static void BuildAllWindows()
        {
            SetupPrefabsAndScene();
            BuildStandalone("Builds/Windows/OutrunAftermath.exe", false);
            BuildStandalone("Builds/WindowsClient/OutrunAftermath_Client.exe", false);
            BuildStandalone("Builds/WindowsServer/OutrunAftermath_Server.exe", true);
        }

        public static void BuildAndroid()
        {
            SetupPrefabsAndScene();

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.outrun.aftermath");
            PlayerSettings.productName = "OUTRUN: AFTERMATH";
            PlayerSettings.companyName = "OutrunStudios";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = true;

            string outDir = "Builds/Android";
            Directory.CreateDirectory(outDir);
            string outputPath = Path.Combine(outDir, "OutrunAftermath.aab").Replace('\\', '/');

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogWarning("[BuildScript] Android Playback Engine is not installed in this Unity Editor. Android build settings configured successfully. Install Android Build Support via Unity Hub to compile AAB/APK.");
                return;
            }

            Debug.Log($"[BuildScript] Initiating Android AAB Build -> {outputPath}...");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
            Debug.Log($"[BuildScript] Android Build Succeeded: {report.summary.totalSize} bytes.");
        }

        public static void BuildIOS()
        {
            SetupPrefabsAndScene();

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.outrun.aftermath");
            PlayerSettings.productName = "OUTRUN: AFTERMATH";
            PlayerSettings.companyName = "OutrunStudios";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            string outDir = "Builds/iOS";
            Directory.CreateDirectory(outDir);

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogWarning("[BuildScript] iOS Playback Engine is not installed in this Unity Editor. iOS build settings configured successfully. Install iOS Build Support via Unity Hub to export Xcode project.");
                return;
            }

            Debug.Log($"[BuildScript] Initiating iOS Xcode Export -> {outDir}...");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = outDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"iOS export failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
            Debug.Log($"[BuildScript] iOS Xcode Export Succeeded.");
        }

        private static void BuildStandalone(string relativeOutputPath, bool isDedicatedServer)
        {
            string outDir = Path.GetDirectoryName(relativeOutputPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string outputPath = relativeOutputPath.Replace('\\', '/');
            Debug.Log($"[BuildScript] Initiating Standalone Build -> {outputPath} (DedicatedServer={isDedicatedServer})...");

            BuildReport report = null;

            if (isDedicatedServer)
            {
                try
                {
                    EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                    var serverOptions = new BuildPlayerOptions
                    {
                        scenes = new[] { MainScenePath },
                        locationPathName = outputPath,
                        target = BuildTarget.StandaloneWindows64,
                        subtarget = (int)StandaloneBuildSubtarget.Server,
                        options = BuildOptions.None
                    };
                    report = BuildPipeline.BuildPlayer(serverOptions);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BuildScript] Dedicated Server module error: {ex.Message}. Falling back to Headless-Safe Standalone Player...");
                }
            }

            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
                var playerOptions = new BuildPlayerOptions
                {
                    scenes = new[] { MainScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    subtarget = (int)StandaloneBuildSubtarget.Player,
                    options = BuildOptions.None
                };
                report = BuildPipeline.BuildPlayer(playerOptions);
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"Build failed for {outputPath}: {summary.result} ({summary.totalErrors} errors)");
            }

            Debug.Log($"[BuildScript] BUILD SUCCEEDED -> {outputPath}! Size: {summary.totalSize} bytes.");
        }

        public static void SetupPrefabsAndScene()
        {
            Debug.Log("[BuildScript] Preparing Prefabs, DefaultNetworkPrefabs, and Main Scene...");

            // 0. Authoritative URP Foundation & Modular 3D Art Assets
            URPSetupPipeline.SetupURP();
            ArtAssetPipeline.ConfigureArtAssets();

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

            // Phase B: Authoritative URP Global Post-Processing Volume
            var globalVolGo = new GameObject("GlobalVolume");
            var globalVol = globalVolGo.AddComponent<UnityEngine.Rendering.Volume>();
            globalVol.isGlobal = true;
            globalVol.profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/DefaultVolumeProfile.asset");

            // EventSystem + InputSystemUIInputModule baked into scene
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            // Save Main.unity
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MainScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
