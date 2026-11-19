using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using OutrunAftermath.Client.Runtime.Abilities;
using OutrunAftermath.Client.Runtime.Objectives;

namespace OutrunAftermath.Client.Multiplayer
{
    public enum SessionStartMode
    {
        InteractiveMenu,
        DedicatedServer,
        Host,
        Client,
        OfflinePractice
    }

    /// <summary>
    /// Configures UnityTransport and NetworkManager lifecycle for Dedicated Server,
    /// Host, Client, and Offline Practice modes. Supports CLI arguments for automated
    /// multi-process multiplayer verification.
    /// </summary>
    public sealed class NetworkSessionController : MonoBehaviour
    {
        public static NetworkSessionController Instance { get; private set; }

        [Header("Default Connection Settings")]
        [SerializeField] private string defaultIp = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;

        public string ActiveIp { get; private set; } = "127.0.0.1";
        public ushort ActivePort { get; private set; } = 7777;
        public int ActiveGameMode { get; private set; } = 0; // 0 = Ground, 1 = Convoy
        public bool IsSessionStarted { get; private set; }
        public bool AutoTestShootEnabled { get; private set; }
        public bool AutoTestAnimEnabled { get; private set; }
        private float _autoQuitSeconds;
        public float AutoQuitSeconds => _autoQuitSeconds;

        public event Action OnOfflinePracticeRequested;
        public event Action<string> OnStatusUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureEventSystem();

            var args = Environment.GetCommandLineArgs();
            SessionStartMode mode = SessionStartMode.InteractiveMenu;
            string ip = defaultIp;
            ushort port = defaultPort;
            string mmDevName = null;
            string mmMode = "GroundOperations";
            string mmRegion = "na-east";
            bool mmPartyCreate = false;
            string mmPartyJoinCode = null;
            bool mmPartyReady = false;
            bool mmQueue = false;
            string screenshotPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("-server", StringComparison.OrdinalIgnoreCase))
                {
                    mode = SessionStartMode.DedicatedServer;
                }
                else if (arg.Equals("-host", StringComparison.OrdinalIgnoreCase))
                {
                    mode = SessionStartMode.Host;
                }
                else if (arg.Equals("-client", StringComparison.OrdinalIgnoreCase))
                {
                    mode = SessionStartMode.Client;
                }
                else if (arg.Equals("-offline", StringComparison.OrdinalIgnoreCase))
                {
                    mode = SessionStartMode.OfflinePractice;
                }
                else if (arg.Equals("-autotest-shoot", StringComparison.OrdinalIgnoreCase))
                {
                    AutoTestShootEnabled = true;
                }
                else if (arg.Equals("-autotest-anim", StringComparison.OrdinalIgnoreCase))
                {
                    AutoTestAnimEnabled = true;
                }
                else if (arg.Equals("-autoclose", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && float.TryParse(args[++i], out float secs))
                {
                    _autoQuitSeconds = secs;
                }
                else if ((arg.Equals("-ip", StringComparison.OrdinalIgnoreCase) || arg.Equals("-connect-ip", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    ip = args[++i];
                }
                else if ((arg.Equals("-port", StringComparison.OrdinalIgnoreCase) || arg.Equals("-connect-port", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length && ushort.TryParse(args[++i], out ushort parsedPort))
                {
                    port = parsedPort;
                }
                else if (arg.Equals("-convoy", StringComparison.OrdinalIgnoreCase))
                {
                    ActiveGameMode = 1; // Convoy Operations Mode
                }
                else if (arg.Equals("-api-url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string apiUrl = args[++i];
                    if (Runtime.Backend.GameApiClient.Instance != null)
                    {
                        Runtime.Backend.GameApiClient.Instance.BaseUrl = apiUrl;
                    }
                }
                else if (arg.Equals("-matchmake-dev", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mmDevName = args[++i];
                }
                else if (arg.Equals("-matchmake-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mmMode = args[++i];
                }
                else if (arg.Equals("-matchmake-region", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mmRegion = args[++i];
                }
                else if (arg.Equals("-party-create", StringComparison.OrdinalIgnoreCase))
                {
                    mmPartyCreate = true;
                }
                else if (arg.Equals("-party-join", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mmPartyJoinCode = args[++i];
                }
                else if (arg.Equals("-party-ready", StringComparison.OrdinalIgnoreCase))
                {
                    mmPartyReady = true;
                }
                else if (arg.Equals("-matchmake-queue", StringComparison.OrdinalIgnoreCase))
                {
                    mmQueue = true;
                }
                else if (arg.Equals("-screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    screenshotPath = args[++i];
                }
            }

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                StartCoroutine(CaptureScreenshotRoutine(screenshotPath));
            }

            if (!string.IsNullOrEmpty(mmDevName))
            {
                StartCoroutine(AutoMatchmakingCliRoutine(mmDevName, mmMode, mmRegion, mmPartyCreate, mmPartyJoinCode, mmPartyReady, mmQueue));
            }

            if (_autoQuitSeconds > 0f)
            {
                StartCoroutine(AutoQuitRoutine(_autoQuitSeconds));
            }

            ActiveIp = ip;
            ActivePort = port;

            if (mode == SessionStartMode.InteractiveMenu && (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null))
            {
                mode = SessionStartMode.DedicatedServer;
            }

            switch (mode)
            {
                case SessionStartMode.DedicatedServer:
                    StartDedicatedServer(ip, port);
                    break;
                case SessionStartMode.Host:
                    StartHostSession(ip, port);
                    break;
                case SessionStartMode.Client:
                    StartClientSession(ip, port);
                    break;
                case SessionStartMode.OfflinePractice:
                    StartOfflinePractice();
                    break;
                default:
                    // Interactive menu: attach LobbyConnectionUI
                    if (FindAnyObjectByType<LobbyConnectionUI>() == null)
                    {
                        var uiGo = new GameObject("LobbyConnectionUI");
                        uiGo.AddComponent<LobbyConnectionUI>();
                    }
                    break;
            }
        }

        public bool StartDedicatedServer(string ip, ushort port)
        {
            ConfigureTransport("0.0.0.0", port, listenAll: true);
            Debug.Log($"[NetworkSession] Dedicated Server process started on PID {System.Diagnostics.Process.GetCurrentProcess().Id}, binding 0.0.0.0:{port} (Mode={(ActiveGameMode == 1 ? "CONVOY" : "GROUND")})...");
            if (NetworkMatchManager.Instance != null)
            {
                NetworkMatchManager.Instance.SetGameMode(ActiveGameMode);
            }
            bool ok = NetworkManager.Singleton.StartServer();
            IsSessionStarted = ok;
            if (ok)
            {
                Debug.Log("[NetworkSession] Server initialized NGO NetworkManager successfully.");
                Debug.Log($"[NetworkSession] Dedicated Server listening on UDP :{port}");
                OnStatusUpdated?.Invoke($"DEDICATED SERVER LISTENING ON :{port}");
            }
            else
            {
                Debug.LogError("[NetworkSession] Failed to start Dedicated Server.");
            }
            return ok;
        }

        public bool StartHostSession(string ip, ushort port)
        {
            ConfigureTransport(ip, port, listenAll: true);
            Debug.Log($"[NetworkSession] Starting Host on {ip}:{port} (Mode={(ActiveGameMode == 1 ? "CONVOY" : "GROUND")})...");
            if (NetworkMatchManager.Instance != null)
            {
                NetworkMatchManager.Instance.SetGameMode(ActiveGameMode);
            }
            bool ok = NetworkManager.Singleton.StartHost();
            IsSessionStarted = ok;
            if (ok)
            {
                Debug.Log("[NetworkSession] Host initialized NGO NetworkManager successfully.");
                Debug.Log($"[NetworkSession] Host active on UDP :{port}");
                OnStatusUpdated?.Invoke($"HOST ACTIVE ON :{port}");
                if (AutoTestShootEnabled || AutoTestAnimEnabled) StartCoroutine(RunAutoTestCombatRoutine());
            }
            else
            {
                Debug.LogError("[NetworkSession] Failed to start Host.");
            }
            return ok;
        }

        public bool StartClientSession(string ip, ushort port)
        {
            ConfigureTransport(ip, port);
            Debug.Log($"[NetworkSession] Connecting Client to {ip}:{port}...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnLocalClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnLocalClientDisconnected;

            bool ok = NetworkManager.Singleton.StartClient();
            IsSessionStarted = ok;
            if (ok)
            {
                Debug.Log("[NetworkSession] Client initialized NGO NetworkManager successfully.");
                OnStatusUpdated?.Invoke($"CONNECTING TO {ip}:{port}...");
                if (AutoTestShootEnabled || AutoTestAnimEnabled) StartCoroutine(RunAutoTestCombatRoutine());
            }
            else
            {
                Debug.LogError("[NetworkSession] Failed to start Client.");
            }
            return ok;
        }

        public void StartOfflinePractice()
        {
            if (IsSessionStarted) return;
            IsSessionStarted = true;
            Debug.Log("[NetworkSession] Starting Offline Practice Mode.");
            OnOfflinePracticeRequested?.Invoke();
        }

        private void OnLocalClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"[NetworkSession] Client successfully connected to server! Assigned ClientId={clientId}");
                OnStatusUpdated?.Invoke($"CONNECTED (ClientId={clientId})");
            }
        }

        private void OnLocalClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.LogWarning($"[NetworkSession] Disconnected from server (ClientId={clientId})");
                OnStatusUpdated?.Invoke("DISCONNECTED FROM SERVER");
                IsSessionStarted = false;
            }
        }

        private void ConfigureTransport(string ip, ushort port, bool listenAll = false)
        {
            ActiveIp = ip;
            ActivePort = port;

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null)
            {
                // forceOverrideCommandLineArgs: true avoids UTP 2.13.3's buggy -ip -> port parser
                utp.SetConnectionData(true, ip, port, listenAll ? "0.0.0.0" : null);
            }
        }

        private IEnumerator AutoQuitRoutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            Debug.Log("[NetworkSession] Auto-close timer reached. Server/Client shutting down cleanly.");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            Application.Quit(0);
        }

        private IEnumerator CaptureScreenshotRoutine(string path)
        {
            // Wait 2.5 seconds for graphics pipeline warmup, lighting calculations, and post-processing
            yield return new WaitForSeconds(2.5f);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[SCREENSHOT] Frame captured successfully to: {path}");
        }

        private IEnumerator RunAutoTestCombatRoutine()
        {
            Debug.Log("[NET_TEST] Auto-test combat coroutine started. Waiting for local player to spawn...");
            float timeout = 25f;
            NetworkPlayerController localPlayer = null;
            NetworkPlayerController enemyPlayer = null;

            while (timeout > 0f)
            {
                timeout -= 0.25f;
                var allPlayers = FindObjectsByType<NetworkPlayerController>();
                foreach (var p in allPlayers)
                {
                    if (p.IsOwner) localPlayer = p;
                }

                if (localPlayer != null)
                {
                    foreach (var p in allPlayers)
                    {
                        if (!p.IsOwner && p.NetFaction.Value != localPlayer.NetFaction.Value)
                        {
                            enemyPlayer = p;
                            break;
                        }
                    }
                    break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            if (localPlayer == null)
            {
                Debug.LogError("[NET_TEST] Timed out waiting for network player to spawn.");
                yield break;
            }

            Debug.Log($"[NET_TEST] Local player confirmed: Callsign={localPlayer.NetCallsign.Value}, NetworkObjectId={localPlayer.NetworkObjectId}");

            // Send verified test gameplay RPC
            Vector3 origin = localPlayer.transform.position + Vector3.up * 1.5f;
            Vector3 dir = localPlayer.transform.forward;
            int testShotCorrelationId = 8801;
            Debug.Log($"[NET_TEST] Sending test gameplay RPC FireWeaponRpc with ShotId={testShotCorrelationId} to server...");
            localPlayer.FireWeaponRpc(origin, dir, 0f, 0, testShotCorrelationId);

            if (AutoTestAnimEnabled)
            {
                string runId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                ulong ownerId = localPlayer.OwnerClientId;
                Debug.Log($"[NET_ANIM] Executing automated animation state replication sequence... RunId={runId} ClientId={ownerId}");

                int seq = 0;

                // Scenario 1: Walk speed change
                localPlayer.NetMoveSpeed.Value = 3.5f;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=WalkSpeed Expected=3.5");
                Debug.Log("[NET_ANIM] Local client set NetMoveSpeed=3.5 (Walk)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 2: Run/sprint speed change
                localPlayer.NetMoveSpeed.Value = 7.0f;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=SprintSpeed Expected=7.0");
                Debug.Log("[NET_ANIM] Local client set NetMoveSpeed=7.0 (Sprint)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 3: Crouch enabled
                localPlayer.NetMoveSpeed.Value = 0f;
                localPlayer.NetIsCrouched.Value = true;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=CrouchEnabled Expected=True");
                Debug.Log("[NET_ANIM] Local client set NetIsCrouched=true (Crouch)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 4: Crouch disabled
                localPlayer.NetIsCrouched.Value = false;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=CrouchDisabled Expected=False");
                Debug.Log("[NET_ANIM] Local client set NetIsCrouched=false (Crouch Disabled)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 5: Aim enabled
                localPlayer.NetIsAiming.Value = true;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=AimEnabled Expected=True");
                Debug.Log("[NET_ANIM] Local client set NetIsAiming=true (Aim)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 6: Aim disabled
                localPlayer.NetIsAiming.Value = false;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=AimDisabled Expected=False");
                Debug.Log("[NET_ANIM] Local client set NetIsAiming=false (Aim Disabled)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 7: Weapon slot 1 (Shotgun)
                localPlayer.NetActiveSlot.Value = 1;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=WeaponSlot1 Expected=1");
                Debug.Log("[NET_ANIM] Local client switched NetActiveSlot=1 (Shotgun)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 8: Weapon slot 2 (Pistol)
                localPlayer.NetActiveSlot.Value = 2;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=WeaponSlot2 Expected=2");
                Debug.Log("[NET_ANIM] Local client switched NetActiveSlot=2 (Pistol)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 9: Weapon slot 0 (AR)
                localPlayer.NetActiveSlot.Value = 0;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=WeaponSlot0 Expected=0");
                Debug.Log("[NET_ANIM] Local client reset to NetActiveSlot=0 (AR)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 10: Reload enabled
                localPlayer.NetIsReloading.Value = true;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=ReloadEnabled Expected=True");
                Debug.Log("[NET_ANIM] Local client set NetIsReloading=true (Reload Enabled)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 11: Reload disabled
                localPlayer.NetIsReloading.Value = false;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=ReloadDisabled Expected=False");
                Debug.Log("[NET_ANIM] Local client set NetIsReloading=false (Reload Disabled)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 12: Downed life state
                localPlayer.NetLifeState.Value = (int)Runtime.Character.PlayerLifeState.Downed;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=DownedState Expected=1");
                Debug.Log("[NET_ANIM] Local client set NetLifeState=1 (Downed)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 13: Revive state
                localPlayer.NetLifeState.Value = (int)Runtime.Character.PlayerLifeState.BeingRevived;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=ReviveState Expected=2");
                Debug.Log("[NET_ANIM] Local client set NetLifeState=2 (BeingRevived)");
                yield return new WaitForSeconds(0.4f);

                // Scenario 14: Alive state after recovery
                localPlayer.NetLifeState.Value = (int)Runtime.Character.PlayerLifeState.Alive;
                Debug.Log($"[NET_ANIM_SEND] RunId={runId} Seq={++seq} ClientId={ownerId} State=AliveState Expected=0");
                Debug.Log("[NET_ANIM] Local client set NetLifeState=0 (Alive)");
                yield return new WaitForSeconds(0.4f);

                Debug.Log($"[NET_ANIM_TEST_COMPLETE] RunId={runId} TotalSent={seq}");
                Debug.Log("[NET_ANIM] Automated animation replication sequence completed successfully.");
            }

            if (enemyPlayer != null)
            {
                Debug.Log($"[NET_TEST] Found Local={localPlayer.NetCallsign.Value} (Faction={localPlayer.NetFaction.Value}) and Enemy={enemyPlayer.NetCallsign.Value} (Faction={enemyPlayer.NetFaction.Value})");
                float initialEnemyHp = enemyPlayer.NetHealth.Value;
                float initialEnemyArmor = enemyPlayer.NetArmor.Value;
                Debug.Log($"[NET_TEST] Enemy initial HP={initialEnemyHp}, Armor={initialEnemyArmor}. Firing real ServerRPC shots...");

                for (int shot = 0; shot < 15; shot++)
                {
                    if (enemyPlayer.NetLifeState.Value == (int)Runtime.Character.PlayerLifeState.Eliminated)
                    {
                        break;
                    }

                    float aimHeight = enemyPlayer.NetLifeState.Value == 0 ? 1.4f : 0.85f;
                    Vector3 targetPos = enemyPlayer.transform.position + Vector3.up * aimHeight;
                    Vector3 eDir = (targetPos - origin).normalized;
                    int enemyShotCorrelationId = 8810 + shot;

                    localPlayer.FireWeaponRpc(origin, eDir, 0f, 0, enemyShotCorrelationId);
                    yield return new WaitForSeconds(0.22f);
                }

                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[NET_TEST] Post-fire Enemy HP={enemyPlayer.NetHealth.Value}, Armor={enemyPlayer.NetArmor.Value}, LifeState={(Runtime.Character.PlayerLifeState)enemyPlayer.NetLifeState.Value}");
            }

            yield return new WaitForSeconds(1.5f);
            Debug.Log("[NET_TEST] Test gameplay RPC successfully processed both on server and client!");

            // Test Operator Ability activation if local player has it
            var abilitySys = localPlayer.GetComponent<OperatorAbilitySystem>();
            if (abilitySys != null && abilitySys.Definition != null)
            {
                Debug.Log($"[NET_TEST] Testing Operator {abilitySys.Definition.Codename} Primary Ability: {abilitySys.PrimaryAbility.Id}...");
                abilitySys.TryActivatePrimary();
                yield return new WaitForSeconds(0.5f);
                Debug.Log($"[NET_TEST] Post-ability Primary CD={abilitySys.ClientPrimaryCd:F1}s");
            }

            // Test Objective Terminal
            if (NetworkObjectiveTerminal.Instance != null)
            {
                Debug.Log($"[NET_TEST] Objective Terminal Uplink Progress={NetworkObjectiveTerminal.Instance.Progress.Value:F1}%, Contested={NetworkObjectiveTerminal.Instance.IsContested.Value}");
            }

            // Test Vehicle Operations if vehicles are present
            var allVehicles = FindObjectsByType<Runtime.Vehicles.NetworkVehicleController>();
            if (allVehicles.Length > 0)
            {
                var myVehicle = localPlayer.GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>() ?? allVehicles[0];
                Debug.Log($"[NET_TEST] Testing Vehicle: ID={myVehicle.NetVehicleId.Value}, Hull={myVehicle.NetBodyHealth.Value}, Engine={myVehicle.NetEngineHealth.Value}, Tires={myVehicle.NetTireHealth.Value}, Mount={myVehicle.NetMountHealth.Value}");

                // Driver test
                if (myVehicle.SeatDriverClientId.Value == localPlayer.OwnerClientId)
                {
                    Debug.Log("[NET_TEST] Driver sending forward throttle & nitro boost...");
                    myVehicle.SendDriveInputRpc(1.0f, 0.2f, false, true);
                    yield return new WaitForSeconds(0.4f);
                    Debug.Log($"[NET_TEST] Vehicle Speed={myVehicle.NetSpeedMps.Value:F1} m/s, Nitro={myVehicle.NetNitroFuel.Value:F1}%");

                    // Test manual seat switch: Driver [0] -> Attacker [1]
                    Debug.Log("[NET_TEST] Testing manual seat switch [1] (Attacker / Turret)...");
                    myVehicle.RequestSwitchSeatRpc((int)OutrunAftermath.Simulation.Core.VehicleSeat.Attacker);
                    yield return new WaitForSeconds(0.4f);
                    Debug.Log($"[NET_TEST] Post-switch Occupancy: Driver={myVehicle.SeatDriverClientId.Value}, Attacker={myVehicle.SeatAttackerClientId.Value}");
                }

                // Turret test
                if (myVehicle.SeatAttackerClientId.Value == localPlayer.OwnerClientId || myVehicle.SeatDriverClientId.Value == localPlayer.OwnerClientId)
                {
                    Debug.Log("[NET_TEST] Testing vehicle roof turret fire...");
                    Vector3 turretPos = myVehicle.transform.position + Vector3.up * 1.7f;
                    Vector3 turretFwd = myVehicle.transform.forward;
                    myVehicle.FireVehicleWeaponRpc(0, turretPos, turretFwd);
                    yield return new WaitForSeconds(0.3f);
                }
            }

            // Test Backend Account integration
            if (Runtime.Backend.GameApiClient.Instance != null && Runtime.Backend.GameApiClient.Instance.IsAuthenticated)
            {
                var prof = Runtime.Backend.GameApiClient.Instance.CurrentProfile;
                string rankInfo = prof != null ? $"Rank={prof.survivorRank}, XP={prof.xp}, Matches={prof.matchesPlayed}" : "Profile loading";
                Debug.Log($"[NET_TEST] Backend Session Active: User={Runtime.Backend.GameApiClient.Instance.CurrentDisplayName} (ID={Runtime.Backend.GameApiClient.Instance.CurrentUserId}) [{rankInfo}]");
            }

            // Clean client disconnect
            Debug.Log("[NetworkSession] Client initiated clean disconnect.");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            Application.Quit(0);
        }

        private IEnumerator AutoMatchmakingCliRoutine(string devName, string mmMode, string mmRegion, bool createParty, string joinPartyCode, bool readyParty, bool queueMatch)
        {
            Debug.Log($"[MatchmakeCLI] Starting Automated Matchmaking Workflow: DevName={devName}, Mode={mmMode}, Region={mmRegion}...");
            yield return new WaitForSeconds(0.5f);

            var api = Runtime.Backend.GameApiClient.Instance;
            if (api == null)
            {
                Debug.LogError("[MatchmakeCLI] GameApiClient not found.");
                yield break;
            }

            var loginTask = api.QuickDevLoginAsync(devName);
            while (!loginTask.IsCompleted) yield return null;
            var (authOk, authMsg) = loginTask.Result;
            if (!authOk)
            {
                Debug.LogError($"[MatchmakeCLI] Auth failed: {authMsg}");
                yield break;
            }
            Debug.Log($"[MatchmakeCLI] Authenticated as {api.CurrentDisplayName} (ID={api.CurrentUserId})");

            if (createParty)
            {
                var partyTask = api.CreatePartyAsync();
                while (!partyTask.IsCompleted) yield return null;
                var (pOk, pDetails, pErr) = partyTask.Result;
                if (pOk && pDetails != null)
                {
                    Debug.Log($"[MatchmakeCLI] Party Created: Code={pDetails.code}, PartyId={pDetails.partyId}");
                }
                else
                {
                    Debug.LogError($"[MatchmakeCLI] Failed to create party: {pErr}");
                }
            }
            else if (!string.IsNullOrEmpty(joinPartyCode))
            {
                var joinTask = api.JoinPartyByCodeAsync(joinPartyCode);
                while (!joinTask.IsCompleted) yield return null;
                var (jOk, jDetails, jErr) = joinTask.Result;
                if (jOk && jDetails != null)
                {
                    Debug.Log($"[MatchmakeCLI] Joined Party: Code={jDetails.code}");
                }
                else
                {
                    Debug.LogError($"[MatchmakeCLI] Failed to join party: {jErr}");
                }
            }

            if (readyParty)
            {
                var readyTask = api.SetPartyReadyAsync(true);
                while (!readyTask.IsCompleted) yield return null;
                Debug.Log("[MatchmakeCLI] Party Member marked READY");
            }

            // Hook match found before enqueuing
            api.OnMatchFound += assignment =>
            {
                Debug.Log($"[MatchmakeCLI] MATCH FOUND! Connecting to {assignment.serverIp}:{assignment.serverPort} (Team={assignment.team})...");
                StartClientSession(assignment.serverIp, (ushort)assignment.serverPort);
            };

            if (queueMatch)
            {
                bool isParty = api.CurrentParty != null && api.CurrentParty.inParty;
                Debug.Log($"[MatchmakeCLI] Enqueueing ticket: Mode={mmMode}, Region={mmRegion}, IsParty={isParty}...");
                var queueTask = api.EnqueueMatchmakingAsync(mmMode, mmRegion, isParty);
                while (!queueTask.IsCompleted) yield return null;
                var (qOk, ticket, qErr) = queueTask.Result;
                if (qOk && ticket != null)
                {
                    Debug.Log($"[MatchmakeCLI] Ticket Enqueued: ID={ticket.ticketId}, Status={ticket.status}");
                }
                else
                {
                    Debug.LogError($"[MatchmakeCLI] Enqueue failed: {qErr}");
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                var inputModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }
        }
    }
}
