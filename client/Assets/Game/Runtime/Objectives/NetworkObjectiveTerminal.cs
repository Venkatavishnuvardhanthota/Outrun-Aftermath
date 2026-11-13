using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Client.Multiplayer;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.UI;

namespace OutrunAftermath.Client.Runtime.Objectives
{
    /// <summary>
    /// Server-authoritative Tactical Objective Terminal for Ground Operations (Dead City Sector 4).
    /// Rogues must infiltrate and complete the Data Uplink hack, while Sentinels defend the terminal.
    /// Manages network progress, contest detection, and holographic zone visuals.
    /// </summary>
    public sealed class NetworkObjectiveTerminal : NetworkBehaviour
    {
        public static NetworkObjectiveTerminal Instance { get; private set; }

        [Header("Objective Configuration")]
        [SerializeField] private float captureRadius = 6.5f;
        [SerializeField] private float hackRatePerSecond = 10f; // 10 seconds to full capture
        [SerializeField] private Vector3 terminalPosition = new Vector3(0f, 0.75f, 65f);

        public NetworkVariable<float> Progress = new NetworkVariable<float>(0f);
        public NetworkVariable<bool> IsContested = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsHacking = new NetworkVariable<bool>(false);
        public NetworkVariable<FixedString64Bytes> ActiveHackerName = new NetworkVariable<FixedString64Bytes>("");

        private LineRenderer _hologramRing;
        private TextMesh _worldLabel;
        private bool _isCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            transform.position = terminalPosition;
            SetupHologramRing();
            SetupWorldLabel();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Progress.OnValueChanged += (_, val) => UpdateClientVisuals();
            IsContested.OnValueChanged += (_, _) => UpdateClientVisuals();
            IsHacking.OnValueChanged += (_, _) => UpdateClientVisuals();

            UpdateClientVisuals();
        }

        private void SetupHologramRing()
        {
            var ringGo = new GameObject("HologramRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, -0.65f, 0f);

            _hologramRing = ringGo.AddComponent<LineRenderer>();
            _hologramRing.useWorldSpace = false;
            _hologramRing.startWidth = 0.08f;
            _hologramRing.endWidth = 0.08f;
            _hologramRing.positionCount = 48;
            _hologramRing.loop = true;

            for (int i = 0; i < 48; i++)
            {
                float angle = i * (Mathf.PI * 2f / 48);
                _hologramRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * captureRadius, 0.05f, Mathf.Sin(angle) * captureRadius));
            }
        }

        private void SetupWorldLabel()
        {
            var labelGo = new GameObject("TerminalLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            _worldLabel = labelGo.AddComponent<TextMesh>();
            _worldLabel.characterSize = 0.07f;
            _worldLabel.fontSize = 32;
            _worldLabel.anchor = TextAnchor.MiddleCenter;
            _worldLabel.alignment = TextAlignment.Center;
            _worldLabel.text = "DATA TERMINAL\n[HOLD E TO HACK]";
        }

        private void Update()
        {
            if (IsServer && !_isCompleted)
            {
                ServerTick();
            }

            // Keep floating label facing local camera
            if (_worldLabel != null && Camera.main != null)
            {
                _worldLabel.transform.rotation = Camera.main.transform.rotation;
            }
        }

        private void ServerTick()
        {
            if (NetworkMatchManager.Instance == null) return;
            if (NetworkMatchManager.Instance.CurrentPhase.Value != (int)MatchPhase.InProgress) return;

            int roguesInZone = 0;
            int sentinelsInZone = 0;
            NetworkPlayerController primaryHacker = null;

            foreach (var p in NetworkMatchManager.Instance.AllPlayers)
            {
                if (p == null) continue;
                var health = p.GetComponent<PlayerHealth>();
                if (health == null || health.State != PlayerLifeState.Alive) continue;

                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= captureRadius)
                {
                    if (p.NetFaction.Value == 0) // Rogue
                    {
                        roguesInZone++;
                        if (primaryHacker == null) primaryHacker = p;
                    }
                    else // Sentinel
                    {
                        sentinelsInZone++;
                    }
                }
            }

            bool contested = roguesInZone > 0 && sentinelsInZone > 0;
            IsContested.Value = contested;

            if (roguesInZone > 0 && !contested)
            {
                // Rogues in zone progress the hack
                IsHacking.Value = true;
                ActiveHackerName.Value = primaryHacker != null ? primaryHacker.NetCallsign.Value : "ROGUE";
                Progress.Value = Mathf.Min(100f, Progress.Value + hackRatePerSecond * Time.deltaTime);

                if (Progress.Value >= 100f)
                {
                    _isCompleted = true;
                    NetworkMatchManager.Instance.ReportObjectiveCompleteServer(Faction.Rogue, "DATA UPLINK SECURED");
                }
            }
            else
            {
                IsHacking.Value = false;
                if (roguesInZone == 0 && Progress.Value > 0f && sentinelsInZone > 0)
                {
                    // Sentinels in zone slowly purge hacked data
                    Progress.Value = Mathf.Max(0f, Progress.Value - (hackRatePerSecond * 0.5f) * Time.deltaTime);
                }
            }
        }

        private void UpdateClientVisuals()
        {
            Color color;
            string text;

            if (_isCompleted || Progress.Value >= 100f)
            {
                color = new Color(0.15f, 0.9f, 0.35f);
                text = "DATA UPLINK COMPLETE\nEXTRACTION CONFIRMED";
            }
            else if (IsContested.Value)
            {
                color = new Color(0.95f, 0.45f, 0.1f);
                text = $"ZONE CONTESTED\n[{Mathf.FloorToInt(Progress.Value)}%]";
            }
            else if (IsHacking.Value)
            {
                color = new Color(0.2f, 0.75f, 1f);
                text = $"TRANSMITTING DATA: {Mathf.FloorToInt(Progress.Value)}%\nBY {ActiveHackerName.Value}";
            }
            else
            {
                color = new Color(0.7f, 0.75f, 0.8f);
                text = $"DATA TERMINAL [{Mathf.FloorToInt(Progress.Value)}%]\nHOLD [E] TO TRANSMIT";
            }

            if (_worldLabel != null)
            {
                _worldLabel.text = text;
                _worldLabel.color = color;
            }

            if (_hologramRing != null)
            {
                _hologramRing.startColor = color;
                _hologramRing.endColor = color;
            }

            // Sync with TacticalHUD
            var hud = FindAnyObjectByType<TacticalHUD>();
            if (hud != null)
            {
                hud.UpdateObjectiveStatus(Progress.Value, IsContested.Value, IsHacking.Value, transform.position);
            }
        }
    }
}
