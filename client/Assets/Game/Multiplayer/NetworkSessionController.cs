using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;

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
        public bool IsSessionStarted { get; private set; }
        public bool AutoTestShootEnabled { get; private set; }
        private float _autoQuitSeconds;

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
            }

            if (_autoQuitSeconds > 0f)
            {
                StartCoroutine(AutoQuitRoutine(_autoQuitSeconds));
            }

            ActiveIp = ip;
            ActivePort = port;

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
            ConfigureTransport("0.0.0.0", port);
            Debug.Log($"[NetworkSession] Starting Dedicated Server on 0.0.0.0:{port}...");
            bool ok = NetworkManager.Singleton.StartServer();
            IsSessionStarted = ok;
            if (ok)
            {
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
            Debug.Log($"[NetworkSession] Starting Host on {ip}:{port}...");
            bool ok = NetworkManager.Singleton.StartHost();
            IsSessionStarted = ok;
            if (ok)
            {
                Debug.Log($"[NetworkSession] Host active on UDP :{port}");
                OnStatusUpdated?.Invoke($"HOST ACTIVE ON :{port}");
                if (AutoTestShootEnabled) StartCoroutine(RunAutoTestCombatRoutine());
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
                OnStatusUpdated?.Invoke($"CONNECTING TO {ip}:{port}...");
                if (AutoTestShootEnabled) StartCoroutine(RunAutoTestCombatRoutine());
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
                Debug.Log($"[NetworkSession] Connected to server! Assigned ClientId={clientId}");
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
            Debug.Log("[NetworkSession] Auto-close timer reached. Shutting down gracefully.");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            Application.Quit(0);
        }

        private IEnumerator RunAutoTestCombatRoutine()
        {
            Debug.Log("[NET_TEST] Auto-test combat coroutine started. Waiting for match in progress...");
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
                }

                if (localPlayer != null && enemyPlayer != null &&
                    NetworkMatchManager.Instance != null &&
                    NetworkMatchManager.Instance.CurrentPhase.Value == (int)MatchPhase.InProgress)
                {
                    break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            if (localPlayer == null || enemyPlayer == null)
            {
                Debug.LogError("[NET_TEST] Timed out waiting for two opposing network players to spawn.");
                yield break;
            }

            Debug.Log($"[NET_TEST] Found Local={localPlayer.NetCallsign.Value} (Faction={localPlayer.NetFaction.Value}) and Enemy={enemyPlayer.NetCallsign.Value} (Faction={enemyPlayer.NetFaction.Value})");

            // Move slightly closer to ensure unobstructed line of sight across Dead City avenue
            float initialEnemyHp = enemyPlayer.NetHealth.Value;
            float initialEnemyArmor = enemyPlayer.NetArmor.Value;
            Debug.Log($"[NET_TEST] Enemy initial HP={initialEnemyHp}, Armor={initialEnemyArmor}. Firing real ServerRPC shots...");

            for (int shot = 0; shot < 8; shot++)
            {
                Vector3 origin = localPlayer.transform.position + Vector3.up * 1.65f;
                Vector3 targetPos = enemyPlayer.transform.position + Vector3.up * 1.55f;
                Vector3 dir = (targetPos - origin).normalized;

                localPlayer.FireWeaponRpc(origin, dir, 0f, 0);
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(0.6f);

            Debug.Log($"[NET_TEST] Post-fire Enemy HP={enemyPlayer.NetHealth.Value}, Armor={enemyPlayer.NetArmor.Value}, LifeState={(Runtime.Character.PlayerLifeState)enemyPlayer.NetLifeState.Value}");
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
