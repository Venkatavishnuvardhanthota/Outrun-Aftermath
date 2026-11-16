using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using OutrunAftermath.Client.Runtime.UI;

namespace OutrunAftermath.Client.Multiplayer
{
    public enum MatchPhase
    {
        WaitingForPlayers = 0,
        InProgress = 1,
        Ended = 2
    }

    /// <summary>
    /// Authoritative multiplayer match coordinator using Netcode for GameObjects.
    /// Handles client connection spawning, balanced Rogue/Sentinel team assignment,
    /// round/match state replication, kill feed broadcasts, and victory evaluation.
    /// </summary>
    public sealed class NetworkMatchManager : NetworkBehaviour
    {
        public static NetworkMatchManager Instance { get; private set; }

        [Header("Player Spawning")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject vehiclePrefab;
        [SerializeField] private float matchDurationSeconds = 600f;
        [SerializeField] private int targetScoreToWin = 5;

        public NetworkVariable<int> ActiveGameMode = new NetworkVariable<int>(0); // 0 = Ground, 1 = Convoy
        public NetworkVariable<Unity.Collections.FixedString32Bytes> NetMapId = new NetworkVariable<Unity.Collections.FixedString32Bytes>("deadcity");
        public NetworkVariable<int> NetWeather = new NetworkVariable<int>((int)Runtime.Environment.WeatherCondition.Clear);
        public NetworkVariable<int> CurrentPhase = new NetworkVariable<int>((int)MatchPhase.WaitingForPlayers);
        public NetworkVariable<int> RogueScore = new NetworkVariable<int>(0);
        public NetworkVariable<int> SentinelScore = new NetworkVariable<int>(0);
        public NetworkVariable<int> RogueCount = new NetworkVariable<int>(0);
        public NetworkVariable<int> SentinelCount = new NetworkVariable<int>(0);
        public NetworkVariable<float> MatchTimer = new NetworkVariable<float>(600f);
        public NetworkVariable<int> WinningFaction = new NetworkVariable<int>(-1);

        private readonly Dictionary<ulong, NetworkPlayerController> _players = new Dictionary<ulong, NetworkPlayerController>();
        private Runtime.Vehicles.NetworkVehicleController _rogueVehicle;
        private Runtime.Vehicles.NetworkVehicleController _sentinelVehicle;
        private float _roundResetTimer;
        private System.DateTime _matchStartTimeUtc = System.DateTime.UtcNow;
        private string _activeMapId = "deadcity";
        private Runtime.Environment.WeatherCondition _activeWeather = Runtime.Environment.WeatherCondition.Clear;

        public Runtime.Vehicles.NetworkVehicleController RogueVehicle => _rogueVehicle;
        public Runtime.Vehicles.NetworkVehicleController SentinelVehicle => _sentinelVehicle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ParseCommandLineArgs();
        }

        private void ParseCommandLineArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-target-score", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int score))
                    {
                        targetScoreToWin = score;
                        Debug.Log($"[NetworkMatchManager] CLI targetScoreToWin set to {targetScoreToWin}");
                    }
                }
                else if (args[i].Equals("-match-duration", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (float.TryParse(args[i + 1], out float dur))
                    {
                        matchDurationSeconds = dur;
                        Debug.Log($"[NetworkMatchManager] CLI matchDurationSeconds set to {matchDurationSeconds}s");
                    }
                }
                else if (args[i].Equals("-map", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _activeMapId = args[i + 1].ToLowerInvariant();
                    Debug.Log($"[NetworkMatchManager] CLI map set to {_activeMapId}");
                }
                else if (args[i].Equals("-weather", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (System.Enum.TryParse<Runtime.Environment.WeatherCondition>(args[i + 1], true, out var weather))
                    {
                        _activeWeather = weather;
                        Debug.Log($"[NetworkMatchManager] CLI weather set to {_activeWeather}");
                    }
                }
            }
        }

        public void SetPlayerPrefab(GameObject prefab)
        {
            playerPrefab = prefab;
        }

        public void SetVehiclePrefab(GameObject prefab)
        {
            vehiclePrefab = prefab;
        }

        private int _configuredGameMode = 0;

        public void SetGameMode(int mode)
        {
            _configuredGameMode = mode;
            if (IsSpawned && IsServer)
            {
                ActiveGameMode.Value = mode;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                ActiveGameMode.Value = _configuredGameMode;
                NetMapId.Value = _activeMapId;
                NetWeather.Value = (int)_activeWeather;
                MatchTimer.Value = matchDurationSeconds;
                CurrentPhase.Value = (int)MatchPhase.WaitingForPlayers;
                WinningFaction.Value = -1;

                if (ActiveGameMode.Value == 1)
                {
                    EnsureConvoyVehiclesServer();
                }

                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // Spawn any clients already connected (e.g. Host's own clientId)
                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    if (!_players.ContainsKey(clientId))
                    {
                        HandleClientConnected(clientId);
                    }
                }
            }

            CurrentPhase.OnValueChanged += (_, _) => RefreshHudStatus();
            RogueScore.OnValueChanged += (_, _) => RefreshHudStatus();
            SentinelScore.OnValueChanged += (_, _) => RefreshHudStatus();
            RogueCount.OnValueChanged += (_, _) => RefreshHudStatus();
            SentinelCount.OnValueChanged += (_, _) => RefreshHudStatus();
            WinningFaction.OnValueChanged += (_, _) => RefreshHudStatus();
            NetMapId.OnValueChanged += (_, _) => ApplyMapAndWeather();
            NetWeather.OnValueChanged += (_, _) => ApplyMapAndWeather();

            ApplyMapAndWeather();
        }

        private void ApplyMapAndWeather()
        {
            if (Runtime.Environment.MapEnvironmentDirector.Instance != null)
            {
                string map = NetMapId.Value.ToString();
                if (string.IsNullOrEmpty(map)) map = "deadcity";
                Runtime.Environment.WeatherCondition weather = (Runtime.Environment.WeatherCondition)NetWeather.Value;
                Runtime.Environment.MapEnvironmentDirector.Instance.LoadMap(map, weather);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (CurrentPhase.Value == (int)MatchPhase.InProgress)
            {
                MatchTimer.Value = Mathf.Max(0f, MatchTimer.Value - Time.deltaTime);
                if (MatchTimer.Value <= 0f)
                {
                    // Time expired: Sentinels defend extraction successfully
                    EndMatchServer(1, "EXTRACTION TIME EXPIRED - ROGUES INTERCEPTED");
                }
            }
            else if (CurrentPhase.Value == (int)MatchPhase.Ended)
            {
                _roundResetTimer -= Time.deltaTime;
                if (_roundResetTimer <= 0f)
                {
                    RestartMatchServer();
                }
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || _players.ContainsKey(clientId)) return;

            if (playerPrefab == null)
            {
                // Fallback lookup from NetworkManager prefab list
                foreach (var entry in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (entry.Prefab != null && entry.Prefab.GetComponent<NetworkPlayerController>() != null)
                    {
                        playerPrefab = entry.Prefab;
                        break;
                    }
                }
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[NetworkMatchManager] Missing NetworkPlayer prefab! Cannot spawn client " + clientId);
                return;
            }

            // Balanced team assignment: Rogues (0) vs Sentinels (1)
            int rogues = 0, sentinels = 0;
            foreach (var p in _players.Values)
            {
                if (p.NetFaction.Value == 0) rogues++;
                else sentinels++;
            }

            int assignedFaction = rogues <= sentinels ? 0 : 1;
            int slotIndex = assignedFaction == 0 ? rogues : sentinels;
            (Vector3 spawnPos, Quaternion spawnRot) = GetFactionSpawnPoint(assignedFaction, slotIndex);

            // Assign distinct operator from verified catalog
            string[] rogueOps = { "rex", "nova", "medic", "torque", "shade" };
            string[] sentinelOps = { "titan", "trace", "viper", "circuit", "falcon" };
            string operatorId = assignedFaction == 0 ? rogueOps[slotIndex % rogueOps.Length] : sentinelOps[slotIndex % sentinelOps.Length];

            var instance = Instantiate(playerPrefab, spawnPos, spawnRot);
            var netObj = instance.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId, true);

            var controller = instance.GetComponent<NetworkPlayerController>();
            string callsign = $"OP-{clientId + 1:00}";
            controller.ServerInitializePlayer(assignedFaction, callsign, operatorId, spawnPos, spawnRot);
            _players[clientId] = controller;

            RecountTeamsServer();

            string factionName = assignedFaction == 0 ? "ROGUE" : "SENTINEL";
            Debug.Log($"[NetworkMatchManager] Spawned client {clientId} as {callsign} ({operatorId.ToUpper()} // {factionName}) at {spawnPos}");
            BroadcastFeedRpc($"{callsign} deployed as {operatorId.ToUpper()} ({factionName})");

            if (ActiveGameMode.Value == 1) // Convoy Operations Mode
            {
                EnsureConvoyVehiclesServer();
                var targetVehicle = assignedFaction == 0 ? _rogueVehicle : _sentinelVehicle;
                if (targetVehicle != null)
                {
                    targetVehicle.BoardPlayerServer(clientId);
                }
            }

            if (_players.Count >= 2 && CurrentPhase.Value == (int)MatchPhase.WaitingForPlayers)
            {
                CurrentPhase.Value = (int)MatchPhase.InProgress;
                _matchStartTimeUtc = System.DateTime.UtcNow;
                string modeMsg = ActiveGameMode.Value == 1 ? "CONVOY PURSUIT LIVE — PROTECT THE TRANSPORT // INTERCEPT HOSTILES" : "MATCH LIVE — ELIMINATE HOSTILE OPERATORS";
                BroadcastFeedRpc(modeMsg);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (_players.TryGetValue(clientId, out var player))
            {
                string name = player != null ? player.NetCallsign.Value.ToString() : $"OP-{clientId + 1:00}";
                _players.Remove(clientId);
                RecountTeamsServer();
                Debug.Log($"[NetworkMatchManager] Client {clientId} ({name}) disconnected.");
                BroadcastFeedRpc($"{name} disconnected");
            }
        }

        public void ReportEliminationServer(ulong victimClientId, ulong attackerClientId, bool isHeadshot)
        {
            if (!IsServer) return;

            _players.TryGetValue(victimClientId, out var victim);
            _players.TryGetValue(attackerClientId, out var attacker);

            string victimName = victim != null ? victim.NetCallsign.Value.ToString() : $"OP-{victimClientId + 1:00}";
            string attackerName = attacker != null ? attacker.NetCallsign.Value.ToString() : "ENVIRONMENT";

            if (attacker != null && victim != null && attacker.NetFaction.Value != victim.NetFaction.Value)
            {
                attacker.AddKillServer();
                if (attacker.NetFaction.Value == 0) RogueScore.Value++;
                else SentinelScore.Value++;
            }

            string hsTag = isHeadshot ? " [HEADSHOT]" : "";
            BroadcastFeedRpc($"{attackerName} eliminated {victimName}{hsTag}");

            if (RogueScore.Value >= targetScoreToWin)
            {
                EndMatchServer(0);
            }
            else if (SentinelScore.Value >= targetScoreToWin)
            {
                EndMatchServer(1);
            }
        }

        public void ReportDownedServer(ulong victimClientId, ulong attackerClientId)
        {
            if (!IsServer) return;

            _players.TryGetValue(victimClientId, out var victim);
            _players.TryGetValue(attackerClientId, out var attacker);

            string victimName = victim != null ? victim.NetCallsign.Value.ToString() : $"OP-{victimClientId + 1:00}";
            string attackerName = attacker != null ? attacker.NetCallsign.Value.ToString() : "ENVIRONMENT";
            BroadcastFeedRpc($"{attackerName} downed {victimName}");
        }

        public void ReportObjectiveCompleteServer(OutrunAftermath.Simulation.Core.Faction faction, string objectiveName)
        {
            if (!IsServer) return;
            int winningFaction = faction == OutrunAftermath.Simulation.Core.Faction.Rogue ? 0 : 1;
            EndMatchServer(winningFaction, objectiveName.ToUpper());
        }

        public void ReportVehicleDestroyedServer(int destroyedFaction)
        {
            if (!IsServer || CurrentPhase.Value != (int)MatchPhase.InProgress) return;

            if (ActiveGameMode.Value == 1) // Convoy Operations
            {
                if (destroyedFaction == 0)
                {
                    SentinelScore.Value += 3;
                    EndMatchServer(1, "ROGUE CONVOY DESTROYED — SENTINEL VICTORY");
                }
                else
                {
                    RogueScore.Value += 3;
                    EndMatchServer(0, "SENTINEL INTERCEPTOR DESTROYED — ROGUE CONVOY ESCAPED");
                }
            }
        }

        private void EnsureConvoyVehiclesServer()
        {
            if (vehiclePrefab == null && NetworkManager.Singleton != null)
            {
                foreach (var entry in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (entry.Prefab != null && entry.Prefab.GetComponent<Runtime.Vehicles.NetworkVehicleController>() != null)
                    {
                        vehiclePrefab = entry.Prefab;
                        break;
                    }
                }
            }

            if (vehiclePrefab == null)
            {
                Debug.LogError("[NetworkMatchManager] Could not find vehiclePrefab in NetworkConfig.Prefabs! Convoy vehicles cannot be spawned.");
                return;
            }

            if (_rogueVehicle == null)
            {
                var rogueGo = Instantiate(vehiclePrefab, new Vector3(-3.5f, 0.45f, -12f), Quaternion.identity);
                var netObj = rogueGo.GetComponent<NetworkObject>();
                netObj.Spawn();
                _rogueVehicle = rogueGo.GetComponent<Runtime.Vehicles.NetworkVehicleController>();
                _rogueVehicle.NetFaction.Value = 0;
                _rogueVehicle.NetVehicleId.Value = "raptor";
                Debug.Log("[NetworkMatchManager] Spawned Rogue Convoy Raptor at " + rogueGo.transform.position);
            }

            if (_sentinelVehicle == null)
            {
                var sentinelGo = Instantiate(vehiclePrefab, new Vector3(3.5f, 0.45f, 40f), Quaternion.Euler(0f, 180f, 0f));
                var netObj = sentinelGo.GetComponent<NetworkObject>();
                netObj.Spawn();
                _sentinelVehicle = sentinelGo.GetComponent<Runtime.Vehicles.NetworkVehicleController>();
                _sentinelVehicle.NetFaction.Value = 1;
                _sentinelVehicle.NetVehicleId.Value = "raptor";
                Debug.Log("[NetworkMatchManager] Spawned Sentinel Interceptor Raptor at " + sentinelGo.transform.position);
            }
        }

        public bool TryGetPlayer(ulong clientId, out NetworkPlayerController controller)
        {
            return _players.TryGetValue(clientId, out controller);
        }

        public IEnumerable<NetworkPlayerController> AllPlayers => _players.Values;

        private void EndMatchServer(int winningFaction, string reason = null)
        {
            if (CurrentPhase.Value == (int)MatchPhase.Ended) return;

            CurrentPhase.Value = (int)MatchPhase.Ended;
            WinningFaction.Value = winningFaction;
            _roundResetTimer = 6f;

            string factionLabel = winningFaction == 0 ? "THE ROGUES" : "THE SENTINELS";
            string reasonSuffix = string.IsNullOrEmpty(reason) ? "" : $" [{reason}]";
            Debug.Log($"[NetworkMatchManager] MATCH ENDED — WINNER: {factionLabel} (Rogue {RogueScore.Value} : Sentinel {SentinelScore.Value}){reasonSuffix}");
            BroadcastFeedRpc($"VICTORY: {factionLabel}{reasonSuffix} ({RogueScore.Value} - {SentinelScore.Value})");

            PlayMatchEndStingerRpc(winningFaction);
            ReportMatchToBackendServer(winningFaction);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayMatchEndStingerRpc(int winner)
        {
            if (Runtime.Audio.AudioManager.Instance == null) return;
            bool isWinner = false;
            if (NetworkManager.Singleton != null && _players.TryGetValue(NetworkManager.Singleton.LocalClientId, out var localPl))
            {
                isWinner = (localPl.NetFaction.Value == winner);
            }
            Runtime.Audio.AudioManager.Instance.PlayMatchStinger(isWinner);
        }

        private void ReportMatchToBackendServer(int winningFaction)
        {
            if (Runtime.Backend.GameApiClient.Instance == null) return;

            var payload = new Runtime.Backend.CompleteMatchRequestDto
            {
                matchId = System.Guid.NewGuid().ToString(),
                mode = ActiveGameMode.Value == 1 ? "ConvoyOperations" : "GroundOperations",
                matchType = "StandardPursuit",
                mapId = NetMapId.Value.ToString(),
                startedAtUtc = _matchStartTimeUtc.ToString("o"),
                winnerFaction = winningFaction == 0 ? "Rogue" : (winningFaction == 1 ? "Sentinel" : "Draw"),
                seed = UnityEngine.Random.Range(1, 999999),
                players = new List<Runtime.Backend.CompleteMatchPlayerDto>()
            };

            foreach (var kvp in _players)
            {
                var p = kvp.Value;
                if (p == null) continue;

                string userIdStr = p.NetUserId.Value.ToString();
                if (string.IsNullOrEmpty(userIdStr) || !System.Guid.TryParse(userIdStr, out _))
                {
                    userIdStr = System.Guid.NewGuid().ToString();
                }

                payload.players.Add(new Runtime.Backend.CompleteMatchPlayerDto
                {
                    userId = userIdStr,
                    faction = p.NetFaction.Value == 0 ? "Rogue" : "Sentinel",
                    operatorId = p.NetOperatorId.Value.ToString(),
                    kills = p.NetKills.Value,
                    revives = 0,
                    objectiveProgress = p.NetFaction.Value == 0 ? (int)(Runtime.Objectives.NetworkObjectiveTerminal.Instance != null ? Runtime.Objectives.NetworkObjectiveTerminal.Instance.Progress.Value : 0) : 0,
                    sideComplete = (p.NetFaction.Value == winningFaction)
                });
            }

            _ = Runtime.Backend.GameApiClient.Instance.CompleteMatchServerAsync(payload);
            BroadcastFeedRpc("MATCH XP & STATS PERSISTED VIA BACKEND");
        }

        private void RestartMatchServer()
        {
            RogueScore.Value = 0;
            SentinelScore.Value = 0;
            WinningFaction.Value = -1;
            MatchTimer.Value = matchDurationSeconds;
            CurrentPhase.Value = _players.Count >= 2 ? (int)MatchPhase.InProgress : (int)MatchPhase.WaitingForPlayers;

            foreach (var kvp in _players)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.ServerForceRespawn();
                }
            }

            BroadcastFeedRpc("NEW OPERATION STARTED");
        }

        private void RecountTeamsServer()
        {
            int rogues = 0, sentinels = 0;
            foreach (var p in _players.Values)
            {
                if (p == null) continue;
                if (p.NetFaction.Value == 0) rogues++;
                else sentinels++;
            }
            RogueCount.Value = rogues;
            SentinelCount.Value = sentinels;
        }

        public static (Vector3 pos, Quaternion rot) GetFactionSpawnPoint(int faction, int slotOffset)
        {
            float xOffset = slotOffset == 0 ? 0f : (((slotOffset % 4) - 1.5f) * 3.5f);
            if (faction == 0)
            {
                // Rogues spawn South Avenue facing North
                return (new Vector3(xOffset, 0.06f, 0f), Quaternion.identity);
            }
            else
            {
                // Sentinels spawn North Avenue facing South
                return (new Vector3(xOffset, 0.06f, 26f), Quaternion.Euler(0f, 180f, 0f));
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void BroadcastFeedRpc(string message)
        {
            var hud = FindAnyObjectByType<TacticalHUD>();
            if (hud != null)
            {
                hud.AddKillFeedMessage(message);
            }
        }

        public void RefreshHudStatus()
        {
            var hud = FindAnyObjectByType<TacticalHUD>();
            if (hud == null) return;

            var phase = (MatchPhase)CurrentPhase.Value;
            if (phase == MatchPhase.Ended)
            {
                string winName = WinningFaction.Value == 0 ? "ROGUES VICTORY" : "SENTINELS VICTORY";
                hud.SetMissionStatus($"{winName} | ROGUES {RogueScore.Value} - {SentinelScore.Value} SENTINELS");
            }
            else
            {
                string phaseLabel = phase == MatchPhase.WaitingForPlayers ? "WAITING FOR PLAYERS" : "COMBAT LIVE";
                hud.SetMissionStatus($"{phaseLabel} | ROGUES ({RogueCount.Value}): {RogueScore.Value}  vs  SENTINELS ({SentinelCount.Value}): {SentinelScore.Value}");
            }
        }
    }
}
