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
        [SerializeField] private float matchDurationSeconds = 600f;
        [SerializeField] private int targetScoreToWin = 5;

        public NetworkVariable<int> CurrentPhase = new NetworkVariable<int>((int)MatchPhase.WaitingForPlayers);
        public NetworkVariable<int> RogueScore = new NetworkVariable<int>(0);
        public NetworkVariable<int> SentinelScore = new NetworkVariable<int>(0);
        public NetworkVariable<int> RogueCount = new NetworkVariable<int>(0);
        public NetworkVariable<int> SentinelCount = new NetworkVariable<int>(0);
        public NetworkVariable<float> MatchTimer = new NetworkVariable<float>(600f);
        public NetworkVariable<int> WinningFaction = new NetworkVariable<int>(-1);

        private readonly Dictionary<ulong, NetworkPlayerController> _players = new Dictionary<ulong, NetworkPlayerController>();
        private float _roundResetTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetPlayerPrefab(GameObject prefab)
        {
            playerPrefab = prefab;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                MatchTimer.Value = matchDurationSeconds;
                CurrentPhase.Value = (int)MatchPhase.WaitingForPlayers;
                WinningFaction.Value = -1;

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
                    // Time expired: higher score wins, or Sentinels defend on tie
                    int winner = RogueScore.Value > SentinelScore.Value ? 0 : 1;
                    EndMatchServer(winner);
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

            var instance = Instantiate(playerPrefab, spawnPos, spawnRot);
            var netObj = instance.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId, true);

            var controller = instance.GetComponent<NetworkPlayerController>();
            string callsign = $"OP-{clientId + 1:00}";
            controller.ServerInitializePlayer(assignedFaction, callsign, spawnPos, spawnRot);
            _players[clientId] = controller;

            RecountTeamsServer();

            string factionName = assignedFaction == 0 ? "ROGUE" : "SENTINEL";
            Debug.Log($"[NetworkMatchManager] Spawned client {clientId} as {callsign} ({factionName}) at {spawnPos}");
            BroadcastFeedRpc($"{callsign} deployed to {factionName}");

            if (_players.Count >= 2 && CurrentPhase.Value == (int)MatchPhase.WaitingForPlayers)
            {
                CurrentPhase.Value = (int)MatchPhase.InProgress;
                BroadcastFeedRpc("MATCH LIVE — ELIMINATE HOSTILE OPERATORS");
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

        public bool TryGetPlayer(ulong clientId, out NetworkPlayerController controller)
        {
            return _players.TryGetValue(clientId, out controller);
        }

        public IEnumerable<NetworkPlayerController> AllPlayers => _players.Values;

        private void EndMatchServer(int winningFaction)
        {
            if (CurrentPhase.Value == (int)MatchPhase.Ended) return;

            CurrentPhase.Value = (int)MatchPhase.Ended;
            WinningFaction.Value = winningFaction;
            _roundResetTimer = 6f;

            string factionLabel = winningFaction == 0 ? "THE ROGUES" : "THE SENTINELS";
            Debug.Log($"[NetworkMatchManager] MATCH ENDED — WINNER: {factionLabel} (Rogue {RogueScore.Value} : Sentinel {SentinelScore.Value})");
            BroadcastFeedRpc($"VICTORY: {factionLabel} ({RogueScore.Value} - {SentinelScore.Value})");
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
            float xOffset = ((slotOffset % 4) - 1.5f) * 3.5f;
            if (faction == 0)
            {
                // Rogues spawn South Avenue facing North
                return (new Vector3(xOffset, 0.06f, -20f), Quaternion.identity);
            }
            else
            {
                // Sentinels spawn North Avenue facing South
                return (new Vector3(xOffset, 0.06f, 44f), Quaternion.Euler(0f, 180f, 0f));
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
