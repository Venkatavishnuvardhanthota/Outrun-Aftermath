using System;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Rendering;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.Abilities;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.UI;
using OutrunAftermath.Simulation.AntiCheat;

namespace OutrunAftermath.Client.Multiplayer
{
    /// <summary>
    /// Networked Operator Controller using Netcode for GameObjects.
    /// Separates owner-predicted movement and viewmodels from server-authoritative
    /// hit registration, anti-cheat fire/origin validation, damage, and lifecycle state.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(WeaponSystem))]
    [RequireComponent(typeof(OperatorAbilitySystem))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        public NetworkVariable<int> NetFaction = new NetworkVariable<int>(0);
        public NetworkVariable<FixedString64Bytes> NetCallsign = new NetworkVariable<FixedString64Bytes>("OPERATOR");
        public NetworkVariable<FixedString64Bytes> NetUserId = new NetworkVariable<FixedString64Bytes>("");
        public NetworkVariable<FixedString32Bytes> NetDisplayName = new NetworkVariable<FixedString32Bytes>("");
        public NetworkVariable<FixedString32Bytes> NetOperatorId = new NetworkVariable<FixedString32Bytes>("rex");
        public NetworkVariable<float> NetHealth = new NetworkVariable<float>(100f);
        public NetworkVariable<float> NetArmor = new NetworkVariable<float>(50f);
        public NetworkVariable<int> NetLifeState = new NetworkVariable<int>((int)PlayerLifeState.Alive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<FixedString64Bytes> NetCorrelationId = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> NetBleedOutRemaining = new NetworkVariable<float>(30f);
        public NetworkVariable<float> NetReviveProgress = new NetworkVariable<float>(0f);
        public NetworkVariable<int> NetKills = new NetworkVariable<int>(0);
        public NetworkVariable<int> NetDeaths = new NetworkVariable<int>(0);

        // Phase C.1: Synchronized animation & stance variables
        public NetworkVariable<float> NetMoveSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> NetIsGrounded = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> NetIsCrouched = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> NetIsAiming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> NetIsReloading = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> NetActiveSlot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private CharacterMotor _motor;
        private PlayerHealth _health;
        private WeaponSystem _weapons;
        private OperatorAbilitySystem _abilitySystem;
        private CameraRig _cameraRig;
        private TacticalHUD _hud;

        private GameObject _visualRoot;
        private OutrunAftermath.Client.Runtime.Character.OperatorVisualController _visualController;
        private readonly List<Renderer> _bodyRenderers = new List<Renderer>();
        private Renderer _torsoRenderer;
        private Renderer _headRenderer;
        private TextMesh _overheadLabel;

        public OutrunAftermath.Client.Runtime.Character.OperatorVisualController VisualController => _visualController;

        // Server-side validation state
        private float _serverNextFireTime;
        private Vector3 _lastServerValidatedPos;
        private ulong _lastAttackerClientId = ulong.MaxValue;
        private bool _lastHitWasHeadshot;
        private int _antiCheatFlags;
        private CameraPerspective _lastAppliedPerspective = (CameraPerspective)(-1);

        // Phase 9: Telemetry & Security Hardening
        private Vector3 _lastAimDirection = Vector3.forward;
        private float _lastAimTimestamp;
        private readonly PacketSequenceTracker _packetTracker = new PacketSequenceTracker(64);
        private uint _clientSequenceCounter;
        private float _nextClientHeartbeatTime;

        public ulong LastAttackerClientId => _lastAttackerClientId;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _health = GetComponent<PlayerHealth>();
            _weapons = GetComponent<WeaponSystem>();
            _abilitySystem = GetComponent<OperatorAbilitySystem>();
            EnsureVisualRig();
        }

        private void EnsureVisualRig()
        {
            if (_visualRoot != null) return;

            string opId = NetOperatorId.Value.IsEmpty ? "rex" : NetOperatorId.Value.ToString().ToLowerInvariant();
            string opUpper = (opId == "trace") ? "TRACE" : "REX";

            var existing = transform.Find("OperatorVisualRig");
            if (existing != null)
            {
                _visualRoot = existing.gameObject;
                _visualController = _visualRoot.GetComponent<OutrunAftermath.Client.Runtime.Character.OperatorVisualController>();
                _torsoRenderer = _visualController != null && _visualController.BodyMesh != null ? _visualController.BodyMesh : existing.Find("Body_Torso")?.GetComponent<Renderer>();
                _headRenderer = existing.Find("Head_Crit")?.GetComponent<Renderer>();
                _overheadLabel = existing.Find("OverheadNameplate")?.GetComponent<TextMesh>();
                _bodyRenderers.Clear();
                foreach (var r in _visualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<TextMesh>() == null) _bodyRenderers.Add(r);
                }
                return;
            }

            // Load Phase C Rigged Operator Prefab from Resources
            string prefabResPath = $"Art/Characters/PF_Operator_{opUpper}";
            var operatorPrefab = Resources.Load<GameObject>(prefabResPath);
            if (operatorPrefab == null)
            {
                Debug.LogWarning($"[NetworkPlayer] Could not find {prefabResPath} in Resources. Falling back to PF_Operator_REX.");
                operatorPrefab = Resources.Load<GameObject>("Art/Characters/PF_Operator_REX");
            }

            if (operatorPrefab != null)
            {
                _visualRoot = Instantiate(operatorPrefab, transform);
                _visualRoot.name = "OperatorVisualRig";
                _visualRoot.transform.localPosition = Vector3.zero;
                _visualRoot.transform.localRotation = Quaternion.identity;

                // Disable CharacterController on visual rig so it doesn't conflict with root CharacterController
                var cc = _visualRoot.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                _visualController = _visualRoot.GetComponent<OutrunAftermath.Client.Runtime.Character.OperatorVisualController>();
                if (_visualController != null)
                {
                    _torsoRenderer = _visualController.BodyMesh;
                }

                // Headshot critical collider on Head bone
                Transform headBone = _visualRoot.transform.Find("ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/Neck/Head");
                if (headBone != null)
                {
                    var headColGo = new GameObject("Head_Crit");
                    headColGo.transform.SetParent(headBone, false);
                    headColGo.transform.localPosition = Vector3.zero;
                    var sc = headColGo.AddComponent<SphereCollider>();
                    sc.radius = 0.16f;
                    _headRenderer = headColGo.GetComponent<Renderer>();
                }
            }
            else
            {
                _visualRoot = new GameObject("OperatorVisualRig");
                _visualRoot.transform.SetParent(transform, false);
            }

            _bodyRenderers.Clear();
            foreach (var r in _visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() == null) _bodyRenderers.Add(r);
            }

            // Overhead Nameplate for Remote Players
            var labelGo = new GameObject("OverheadNameplate");
            labelGo.transform.SetParent(_visualRoot.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            _overheadLabel = labelGo.AddComponent<TextMesh>();
            _overheadLabel.characterSize = 0.08f;
            _overheadLabel.fontSize = 36;
            _overheadLabel.anchor = TextAnchor.MiddleCenter;
            _overheadLabel.alignment = TextAlignment.Center;
            _overheadLabel.text = "OPERATOR";
        }

        public void RebuildVisualRig(string newOpId)
        {
            if (_visualRoot != null)
            {
                Destroy(_visualRoot);
                _visualRoot = null;
                _visualController = null;
                _torsoRenderer = null;
                _headRenderer = null;
                _overheadLabel = null;
                _bodyRenderers.Clear();
            }

            EnsureVisualRig();

            if (_visualController != null)
            {
                string faction = NetFaction.Value == 0 ? "Rogue" : "Sentinel";
                _visualController.Initialize(newOpId.ToLowerInvariant(), faction, _motor, _weapons, _health, _cameraRig, IsOwner);
                if (_cameraRig != null)
                {
                    _visualController.SetPerspective(_cameraRig.Perspective);
                }
            }
        }

        public void ServerInitializePlayer(int faction, string callsign, string operatorId, Vector3 spawnPos, Quaternion spawnRot)
        {
            if (!IsServer) return;

            NetFaction.Value = faction;
            NetCallsign.Value = new FixedString64Bytes(callsign);
            NetOperatorId.Value = new FixedString32Bytes(operatorId);
            NetHealth.Value = _health.MaxHealth;
            NetArmor.Value = _health.MaxArmor;
            NetLifeState.Value = (int)PlayerLifeState.Alive;
            NetBleedOutRemaining.Value = 30f;
            NetReviveProgress.Value = 0f;

            if (_abilitySystem != null)
            {
                _abilitySystem.SetOperator(operatorId);
            }

            _health.SetSpawnPoint(spawnPos, spawnRot);
            _lastServerValidatedPos = spawnPos;
        }

        public void ServerForceRespawn()
        {
            if (!IsServer) return;

            _health.Respawn();
            SyncHealthVariablesFromServer();
            TeleportOwnerRpc(_health.transform.position, _health.transform.rotation);
        }

        public void AddKillServer()
        {
            if (!IsServer) return;
            NetKills.Value++;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[NetworkPlayer] Client/Server NetworkPlayer spawned! NetworkObjectId={NetworkObjectId}, OwnerClientId={OwnerClientId}, IsOwner={IsOwner}, IsServer={IsServer}");

            // Subscribe to state replication
            NetFaction.OnValueChanged += (_, _) => UpdateFactionVisuals();
            NetCallsign.OnValueChanged += (_, _) => UpdateOverheadLabel();
            NetOperatorId.OnValueChanged += (_, newId) =>
            {
                if (_abilitySystem != null) _abilitySystem.SetOperator(newId.ToString());
                string currentOp = _visualController != null ? _visualController.OperatorId : "";
                if (!string.Equals(currentOp, newId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    RebuildVisualRig(newId.ToString());
                }
                UpdateFactionVisuals();
                UpdateOverheadLabel();
            };
            NetHealth.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetArmor.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetLifeState.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetBleedOutRemaining.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetReviveProgress.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetActiveSlot.OnValueChanged += (_, newSlot) =>
            {
                if (_visualController != null)
                {
                    _visualController.SetActiveSlotIndex(newSlot);
                }
            };

            UpdateFactionVisuals();
            UpdateOverheadLabel();

            if (IsServer)
            {
                _health.IsServerControlled = false;
                _health.OnHealthArmorChanged += (_, _) => SyncHealthVariablesFromServer();
                _health.OnDowned += HandleServerDowned;
                _health.OnEliminated += HandleServerEliminated;
                _health.OnRevived += HandleServerRevived;
                _lastServerValidatedPos = transform.position;
            }
            else
            {
                _health.IsServerControlled = true;
            }

            if (IsOwner)
            {
                SetupLocalOwnerSystems();
            }
            else
            {
                // Disable local CharacterController collision interference on remote replicas
                // Keep capsule/sphere colliders active so shots hit remote players!
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                if (_visualController != null)
                {
                    string opUpper = (NetOperatorId.Value.IsEmpty ? "REX" : NetOperatorId.Value.ToString()).ToUpperInvariant();
                    string faction = NetFaction.Value == 0 ? "Rogue" : "Sentinel";
                    _visualController.Initialize(opUpper.ToLower(), faction, _motor, _weapons, _health, null, false);
                    _visualController.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                    _visualController.SetActiveSlotIndex(NetActiveSlot.Value);
                }
            }

            OnReplicatedHealthChanged();
        }

        private void SetupLocalOwnerSystems()
        {
            if (FindAnyObjectByType<InputManager>() == null)
            {
                var inputGo = new GameObject("InputManager");
                inputGo.AddComponent<InputManager>();
            }

            var camRigGo = new GameObject("PlayerCameraRig");
            _cameraRig = camRigGo.AddComponent<CameraRig>();
            _cameraRig.Initialize(transform);

            _motor.Initialize(_cameraRig);
            _weapons.Initialize(_cameraRig, _motor, _health);
            _weapons.IsNetworkManaged = true;
            _weapons.OnNetworkFireRequested += HandleLocalFireRequest;

            if (_visualController != null)
            {
                string opUpper = (NetOperatorId.Value.IsEmpty ? "REX" : NetOperatorId.Value.ToString()).ToUpperInvariant();
                string opFaction = NetFaction.Value == 0 ? "Rogue" : "Sentinel";
                _visualController.Initialize(opUpper.ToLower(), opFaction, _motor, _weapons, _health, _cameraRig, true);
                _visualController.SetPerspective(_cameraRig.Perspective);
            }

            // Remove any pre-existing HUD and bind ours
            var existingHud = FindAnyObjectByType<TacticalHUD>();
            if (existingHud != null) Destroy(existingHud.gameObject);

            var hudGo = new GameObject("TacticalHUD");
            _hud = hudGo.AddComponent<TacticalHUD>();
            _hud.Initialize(_health, _weapons, _cameraRig);
            if (_abilitySystem != null)
            {
                _hud.BindAbilitySystem(_abilitySystem);
            }

            string role = IsHost ? "HOST" : "CLIENT";
            string faction = NetFaction.Value == 0 ? "ROGUE" : "SENTINEL";
            _hud.SetNetworkStatus($"[{role} | {NetCallsign.Value} | {faction}]");

            if (_overheadLabel != null)
            {
                _overheadLabel.gameObject.SetActive(false);
            }

            if (NetworkMatchManager.Instance != null)
            {
                NetworkMatchManager.Instance.RefreshHudStatus();
            }

            // Sync persistent player identity from GameApiClient
            if (Runtime.Backend.GameApiClient.Instance != null)
            {
                if (Runtime.Backend.GameApiClient.Instance.IsAuthenticated)
                {
                    RegisterAccountRpc(Runtime.Backend.GameApiClient.Instance.CurrentUserId, Runtime.Backend.GameApiClient.Instance.CurrentDisplayName);
                }
                else
                {
                    RegisterAccountRpc(System.Guid.NewGuid().ToString(), NetCallsign.Value.ToString());
                    Runtime.Backend.GameApiClient.Instance.OnAuthStatusChanged += HandleAuthStatusChanged;
                }
            }
            else
            {
                RegisterAccountRpc(System.Guid.NewGuid().ToString(), NetCallsign.Value.ToString());
            }

            UpdateLocalBodyVisibility();
        }

        private void HandleAuthStatusChanged(bool isAuthenticated, string message)
        {
            if (isAuthenticated && IsOwner && Runtime.Backend.GameApiClient.Instance != null)
            {
                RegisterAccountRpc(Runtime.Backend.GameApiClient.Instance.CurrentUserId, Runtime.Backend.GameApiClient.Instance.CurrentDisplayName);
            }
        }

        public override void OnDestroy()
        {
            if (Runtime.Backend.GameApiClient.Instance != null)
            {
                Runtime.Backend.GameApiClient.Instance.OnAuthStatusChanged -= HandleAuthStatusChanged;
            }
            base.OnDestroy();
        }

        [Rpc(SendTo.Server)]
        public void RegisterAccountRpc(string userId, string displayName)
        {
            if (!IsServer) return;
            NetUserId.Value = userId;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                NetDisplayName.Value = displayName;
                NetCallsign.Value = displayName;
            }
            Debug.Log($"[NetworkPlayer] Registered account for client {OwnerClientId}: {displayName} ({userId})");
        }

        [Rpc(SendTo.Server)]
        public void RequestSetLifeStateRpc(int lifeState, FixedString64Bytes corrId)
        {
            if (!IsServer) return;
            NetLifeState.Value = lifeState;
            if (_health != null)
            {
                _health.ApplyReplicatedState(
                    lifeState == (int)PlayerLifeState.Alive ? 100f : (lifeState == (int)PlayerLifeState.Eliminated ? 0f : _health.CurrentHealth),
                    _health.CurrentArmor,
                    (PlayerLifeState)lifeState,
                    _health.BleedOutRemaining,
                    _health.ReviveProgress);
            }
            string sName = lifeState == 1 ? "DownedState" : (lifeState == 2 ? "ReviveState" : (lifeState == 3 ? "DeathState" : "AliveState"));
            string corr = corrId.IsEmpty ? "None" : corrId.ToString();
            Debug.Log($"[NET_ANIM_SERVER] CorrId={corr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
        }

        [Rpc(SendTo.Server)]
        public void RequestRespawnRpc(FixedString64Bytes corrId)
        {
            if (!IsServer) return;
            NetLifeState.Value = (int)PlayerLifeState.Alive;
            NetActiveSlot.Value = 0;
            NetMoveSpeed.Value = 0f;
            NetIsCrouched.Value = false;
            NetIsAiming.Value = false;
            NetIsReloading.Value = false;
            if (_health != null)
            {
                _health.Respawn();
            }
            string corr = corrId.IsEmpty ? "None" : corrId.ToString();
            Debug.Log($"[NET_ANIM_SERVER] CorrId={corr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State=RespawnAction Speed=0.0 Crouch=False Aim=False Slot=0 Reload=False LifeState={NetLifeState.Value}");
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                ServerTickValidation();
            }

            if (IsOwner)
            {
                OwnerTick();
            }
            else
            {
                // Keep remote nameplate facing local camera
                if (_overheadLabel != null && Camera.main != null)
                {
                    _overheadLabel.transform.rotation = Camera.main.transform.rotation;
                }

                // Phase C.1: Update remote proxy animation parameters from synchronized network variables
                SynchronizeRemoteAnimationParameters();
            }
        }

        private float _lastLoggedSpeed = -1f;
        private int _lastLoggedSlot = -1;
        private bool _lastLoggedCrouch = false;
        private bool _lastLoggedAim = false;
        private bool _lastLoggedReload = false;
        private int _lastLoggedLifeState = -1;

        private float _lastServerLoggedSpeed = -1f;
        private int _lastServerLoggedSlot = -1;
        private bool _lastServerLoggedCrouch = false;
        private bool _lastServerLoggedAim = false;
        private bool _lastServerLoggedReload = false;
        private int _lastServerLoggedLifeState = -1;

        public void SynchronizeRemoteAnimationParameters()
        {
            if (_visualController != null)
            {
                bool isReviving = NetLifeState.Value == (int)PlayerLifeState.BeingRevived;
                _visualController.UpdateRemoteAnimationParameters(
                    NetMoveSpeed.Value,
                    NetIsGrounded.Value,
                    NetIsCrouched.Value,
                    NetIsAiming.Value,
                    NetIsReloading.Value,
                    NetActiveSlot.Value,
                    NetLifeState.Value,
                    isReviving
                );
            }

            if (!IsOwner)
            {
                string corr = NetCorrelationId.Value.IsEmpty ? "None" : NetCorrelationId.Value.ToString();
                bool speedChanged = Mathf.Abs(NetMoveSpeed.Value - _lastLoggedSpeed) > 0.1f;
                bool slotChanged = NetActiveSlot.Value != _lastLoggedSlot;
                bool crouchChanged = NetIsCrouched.Value != _lastLoggedCrouch;
                bool aimChanged = NetIsAiming.Value != _lastLoggedAim;
                bool reloadChanged = NetIsReloading.Value != _lastLoggedReload;
                bool lifeChanged = NetLifeState.Value != _lastLoggedLifeState;

                if (speedChanged)
                {
                    _lastLoggedSpeed = NetMoveSpeed.Value;
                    string sName = NetMoveSpeed.Value >= 6.0f ? "SprintSpeed" : (NetMoveSpeed.Value >= 4.5f ? "RunSpeed" : (NetMoveSpeed.Value >= 2.0f ? "WalkSpeed" : "IdleSpeed"));
                    string sVal = NetMoveSpeed.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (crouchChanged)
                {
                    _lastLoggedCrouch = NetIsCrouched.Value;
                    string sName = NetIsCrouched.Value ? "CrouchEnabled" : "CrouchDisabled";
                    string sVal = NetIsCrouched.Value.ToString();
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (aimChanged)
                {
                    _lastLoggedAim = NetIsAiming.Value;
                    string sName = NetIsAiming.Value ? "AimEnabled" : "AimDisabled";
                    string sVal = NetIsAiming.Value.ToString();
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (slotChanged)
                {
                    _lastLoggedSlot = NetActiveSlot.Value;
                    string sName = $"WeaponSlot{NetActiveSlot.Value}";
                    string sVal = NetActiveSlot.Value.ToString();
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (reloadChanged)
                {
                    _lastLoggedReload = NetIsReloading.Value;
                    string sName = NetIsReloading.Value ? "ReloadEnabled" : "ReloadDisabled";
                    string sVal = NetIsReloading.Value.ToString();
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (lifeChanged)
                {
                    _lastLoggedLifeState = NetLifeState.Value;
                    string sName = NetLifeState.Value == 1 ? "DownedState" : (NetLifeState.Value == 2 ? "ReviveState" : (NetLifeState.Value == 3 ? "DeathState" : "AliveState"));
                    string sVal = NetLifeState.Value.ToString();
                    Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Value={sVal} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (speedChanged || slotChanged || crouchChanged || aimChanged || reloadChanged || lifeChanged)
                {
                    Debug.Log($"[NET_ANIM_REPL] CorrId={corr} Proxy ClientId={OwnerClientId} CallSign={NetCallsign.Value} replicated: Speed={NetMoveSpeed.Value:F1}, Crouch={NetIsCrouched.Value}, Aim={NetIsAiming.Value}, Slot={NetActiveSlot.Value}, LifeState={NetLifeState.Value}");
                }
            }

            if (IsServer && !IsOwner)
            {
                string sCorr = NetCorrelationId.Value.IsEmpty ? "None" : NetCorrelationId.Value.ToString();
                bool sSpeedChanged = Mathf.Abs(NetMoveSpeed.Value - _lastServerLoggedSpeed) > 0.1f;
                bool sSlotChanged = NetActiveSlot.Value != _lastServerLoggedSlot;
                bool sCrouchChanged = NetIsCrouched.Value != _lastServerLoggedCrouch;
                bool sAimChanged = NetIsAiming.Value != _lastServerLoggedAim;
                bool sReloadChanged = NetIsReloading.Value != _lastServerLoggedReload;
                bool sLifeChanged = NetLifeState.Value != _lastServerLoggedLifeState;

                if (sSpeedChanged)
                {
                    _lastServerLoggedSpeed = NetMoveSpeed.Value;
                    string sName = NetMoveSpeed.Value >= 6.0f ? "SprintSpeed" : (NetMoveSpeed.Value >= 4.5f ? "RunSpeed" : (NetMoveSpeed.Value >= 2.0f ? "WalkSpeed" : "IdleSpeed"));
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (sCrouchChanged)
                {
                    _lastServerLoggedCrouch = NetIsCrouched.Value;
                    string sName = NetIsCrouched.Value ? "CrouchEnabled" : "CrouchDisabled";
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (sAimChanged)
                {
                    _lastServerLoggedAim = NetIsAiming.Value;
                    string sName = NetIsAiming.Value ? "AimEnabled" : "AimDisabled";
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (sSlotChanged)
                {
                    _lastServerLoggedSlot = NetActiveSlot.Value;
                    string sName = $"WeaponSlot{NetActiveSlot.Value}";
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (sReloadChanged)
                {
                    _lastServerLoggedReload = NetIsReloading.Value;
                    string sName = NetIsReloading.Value ? "ReloadEnabled" : "ReloadDisabled";
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
                if (sLifeChanged)
                {
                    _lastServerLoggedLifeState = NetLifeState.Value;
                    string sName = NetLifeState.Value == 1 ? "DownedState" : (NetLifeState.Value == 2 ? "ReviveState" : (NetLifeState.Value == 3 ? "DeathState" : "AliveState"));
                    Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State={sName} Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
                }
            }
        }

        private void OwnerTick()
        {
            if (InputManager.Instance == null) return;
            var input = InputManager.Instance.Current;

            if (input.TogglePerspective && _cameraRig != null)
            {
                _cameraRig.TogglePerspective();
                UpdateLocalBodyVisibility();
            }

            if (_cameraRig != null)
            {
                _cameraRig.AddLookInput(input.Look);
                _cameraRig.SetAiming(input.Aim);
            }

            bool isInsideVehicle = GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>() != null;

            // Revive request while downed or near downed ally
            if (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived)
            {
                if (input.Interact)
                {
                    RequestReviveRpc(OwnerClientId);
                }
            }
            else if (input.Interact && _health.State == PlayerLifeState.Alive && !isInsideVehicle)
            {
                // 1. Check nearby vehicles to board
                var nearbyVehicles = FindObjectsByType<Runtime.Vehicles.NetworkVehicleController>();
                bool boarded = false;
                foreach (var v in nearbyVehicles)
                {
                    if (v != null && !v.NetIsExploded.Value && Vector3.Distance(transform.position, v.transform.position) <= 4.2f)
                    {
                        v.RequestBoardVehicleRpc();
                        boarded = true;
                        break;
                    }
                }

                if (!boarded)
                {
                    // 2. Check nearby downed teammates
                    RequestNearbyAllyReviveRpc();
                }
            }

            bool canMove = !isInsideVehicle && (_health.State == PlayerLifeState.Alive || _health.State == PlayerLifeState.Downed);
            _motor.ProcessMovement(input, canMove);

            if (!isInsideVehicle)
            {
                _weapons.ProcessWeapons(input);
            }

            // Phase C.1: Synchronize local player animation variables to remote proxies
            bool isAutomatedAnimTest = NetworkSessionController.Instance != null && NetworkSessionController.Instance.AutoTestAnimEnabled;
            if (!isAutomatedAnimTest)
            {
                if (_motor != null)
                {
                    NetMoveSpeed.Value = _motor.Velocity.magnitude;
                    NetIsGrounded.Value = _motor.IsGrounded;
                    NetIsCrouched.Value = (_motor.Stance == MovementStance.Crouching || _motor.Stance == MovementStance.Sliding);
                }
                if (_cameraRig != null)
                {
                    NetIsAiming.Value = _cameraRig.IsAiming;
                }
                if (_weapons != null)
                {
                    NetIsReloading.Value = _weapons.IsReloading;
                    NetActiveSlot.Value = _weapons.ActiveSlotIndex;
                }
            }

            // Trigger Abilities
            if (_abilitySystem != null && _health.State == PlayerLifeState.Alive)
            {
                if (input.AbilityPrimary)
                {
                    _abilitySystem.TryActivatePrimary();
                }
                if (input.AbilityUltimate)
                {
                    _abilitySystem.TryActivateUltimate();
                }
            }

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            // Phase 9: Client heartbeat packet sequence transmission
            if (IsOwner && Time.time >= _nextClientHeartbeatTime)
            {
                _nextClientHeartbeatTime = Time.time + 1.0f;
                _clientSequenceCounter++;
                HeartbeatServerRpc(_clientSequenceCounter, Time.time);
            }
        }

        private void ServerTickValidation()
        {
            // Keep NetBleedOutRemaining / ReviveProgress synced when downed
            if (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived)
            {
                NetBleedOutRemaining.Value = _health.BleedOutRemaining;
                NetReviveProgress.Value = _health.ReviveProgress;
            }

            // Anti-cheat movement displacement monitor with authoritative server snap-back
            float maxDistPerSec = SpeedLimits.AbsoluteMaxDisplacementPerSec;
            float dt = Mathf.Max(Time.deltaTime, 0.02f);
            float allowedStep = maxDistPerSec * dt + 2.5f;
            float dist = Vector3.Distance(transform.position, _lastServerValidatedPos);
            if (dist > allowedStep && _health.State != PlayerLifeState.Eliminated)
            {
                _antiCheatFlags++;
                Debug.LogWarning($"[AntiCheat:SecurityViolation] Teleport/Speed anomaly for Client {OwnerClientId}: displacement {dist:F2}m > max {allowedStep:F2}m. Reverting position.");
                transform.position = _lastServerValidatedPos;
                CorrectPositionClientRpc(_lastServerValidatedPos);
            }
            else
            {
                _lastServerValidatedPos = transform.position;
            }
        }

        private static int _nextCorrelationId = 1000;

        private void HandleLocalFireRequest(Ray aimRay, float spreadDeg, int slotIndex)
        {
            int correlationId = System.Threading.Interlocked.Increment(ref _nextCorrelationId);
            Debug.Log($"[NetworkPlayer] Client sending FireWeaponRpc: ClientId={OwnerClientId}, WeaponSlot={slotIndex}, ShotId={correlationId}");
            FireWeaponRpc(aimRay.origin, aimRay.direction, spreadDeg, slotIndex, correlationId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void FireWeaponRpc(Vector3 origin, Vector3 direction, float clientSpreadDeg, int weaponSlot, int shotCorrelationId = 0)
        {
            Debug.Log($"[NetworkPlayer] Server processed gameplay RPC: FireWeaponRpc from ClientId={OwnerClientId}, WeaponSlot={weaponSlot}, ShotId={shotCorrelationId}, Origin={origin}");
            string sCorr = NetCorrelationId.Value.IsEmpty ? "None" : NetCorrelationId.Value.ToString();
            Debug.Log($"[NET_ANIM_SERVER] CorrId={sCorr} ClientId={OwnerClientId} CallSign={NetCallsign.Value} State=FireAction Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");

            if (_health.State != PlayerLifeState.Alive) return;
            if (weaponSlot < 0 || weaponSlot >= _weapons.Loadout.Count) return;

            var weapon = _weapons.Loadout[weaponSlot];

            // Server anti-cheat: fire rate check (with 40ms jitter tolerance)
            if (Time.time < _serverNextFireTime - 0.04f)
            {
                _antiCheatFlags++;
                Debug.LogWarning($"[AntiCheat] Fire rate violation by client {OwnerClientId}");
                return;
            }

            // Server anti-cheat: shot origin must be near player's body/camera pivot
            Vector3 playerEyeApprox = transform.position + Vector3.up * 1.4f;
            if (Vector3.Distance(origin, playerEyeApprox) > 3.2f)
            {
                _antiCheatFlags++;
                Debug.LogWarning($"[AntiCheat] Invalid shot origin ({Vector3.Distance(origin, playerEyeApprox):F2}m) from client {OwnerClientId}");
                origin = playerEyeApprox;
            }

            _serverNextFireTime = Time.time + (60f / weapon.roundsPerMinute);

            Vector3 normDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            float clampedSpread = Mathf.Clamp(clientSpreadDeg, 0f, weapon.maxSpread);

            // Server anti-cheat: aim-snap rotational velocity analysis
            float aimDt = Time.time - _lastAimTimestamp;
            Vec3 prevAim = new Vec3(_lastAimDirection.x, _lastAimDirection.y, _lastAimDirection.z);
            Vec3 currAim = new Vec3(normDir.x, normDir.y, normDir.z);
            bool isAimSnap = AimBotDetector.IsSuspiciousSnap(prevAim, currAim, aimDt, out float angularVel);
            if (isAimSnap)
            {
                _antiCheatFlags++;
                Debug.LogWarning($"[AntiCheat:AimSnap] Suspicious aim snap ({angularVel:F0} deg/s) detected from Client {OwnerClientId}. Headshot bonus stripped.");
            }
            _lastAimDirection = normDir;
            _lastAimTimestamp = Time.time;

            bool anyHit = false;
            bool anyHeadshot = false;
            Vector3 firstTracerEnd = origin + normDir * weapon.range;

            for (int i = 0; i < weapon.pelletsPerShot; i++)
            {
                Vector3 pelletDir = ApplyServerSpread(normDir, clampedSpread);
                Ray ray = new Ray(origin, pelletDir);

                if (TryRaycastExcludingSelf(ray, weapon.range, out RaycastHit hit))
                {
                    if (i == 0) firstTracerEnd = hit.point;

                    bool isHeadshot = hit.collider.name.ToLower().Contains("head");
                    if (isHeadshot && isAimSnap) isHeadshot = false; // Strip headshot bonus on suspicious aim snap
                    float dmg = isHeadshot ? weapon.damage * weapon.headshotMultiplier : weapon.damage;
                    float falloff = 1f - Mathf.Clamp01(hit.distance / weapon.range) * 0.45f;
                    dmg *= falloff;

                    var hitTargetPlayer = hit.collider.GetComponentInParent<NetworkPlayerController>();
                    if (hitTargetPlayer != null && hitTargetPlayer != this)
                    {
                        anyHit = true;
                        if (isHeadshot) anyHeadshot = true;
                        hitTargetPlayer.ApplyServerDamage(dmg, hit.point, OwnerClientId, isHeadshot);
                    }
                    else
                    {
                        var hazard = hit.collider.GetComponentInParent<Runtime.Environment.EnvironmentalHazard>();
                        if (hazard != null)
                        {
                            anyHit = true;
                            hazard.TakeDamage(dmg, hit.point, normDir);
                        }
                        else
                        {
                            var damageable = hit.collider.GetComponentInParent<IDamageable>();
                            if (damageable != null)
                            {
                                anyHit = true;
                                if (isHeadshot) anyHeadshot = true;
                                damageable.TakeDamage(dmg, hit.point, hit.normal, isHeadshot);
                            }
                        }
                    }
                }
                else if (i == 0)
                {
                    firstTracerEnd = origin + pelletDir * weapon.range;
                }
            }

            Debug.Log($"[NetworkPlayer] Server broadcasting PlayShotEffectsRpc: ClientId={OwnerClientId}, ShotId={shotCorrelationId}, Start={_weapons.MuzzleWorldPosition}, End={firstTracerEnd}, Hit={anyHit}");
            PlayShotEffectsRpc(_weapons.MuzzleWorldPosition, firstTracerEnd, anyHit, anyHeadshot, shotCorrelationId);
        }

        private bool TryRaycastExcludingSelf(Ray ray, float maxDist, out RaycastHit closestHit)
        {
            var hits = Physics.RaycastAll(ray, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            closestHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.transform.IsChildOf(transform)) continue;

                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    closestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        private Vector3 ApplyServerSpread(Vector3 forward, float spreadDegrees)
        {
            if (spreadDegrees <= 0.01f) return forward;
            float radius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, forward).normalized;
            return (forward + right * circle.x + up * circle.y).normalized;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayShotEffectsRpc(Vector3 tracerStart, Vector3 tracerEnd, bool didHit, bool isHeadshot, int shotCorrelationId = 0)
        {
            Debug.Log($"[NetworkPlayer] Client processed PlayShotEffectsRpc from server confirming shot fired! ShotId={shotCorrelationId}, Start={tracerStart}, End={tracerEnd}, Hit={didHit}");

            if (!IsOwner)
            {
                string corr = NetCorrelationId.Value.IsEmpty ? "None" : NetCorrelationId.Value.ToString();
                Debug.Log($"[NET_ANIM_RECV] CorrId={corr} TargetClientId={OwnerClientId} CallSign={NetCallsign.Value} State=FireAction Value=True Speed={NetMoveSpeed.Value:F1} Crouch={NetIsCrouched.Value} Aim={NetIsAiming.Value} Slot={NetActiveSlot.Value} Reload={NetIsReloading.Value} LifeState={NetLifeState.Value}");
            }

            if (_weapons != null)
            {
                _weapons.SpawnVisualTracer(tracerStart, tracerEnd);
                if (IsOwner && didHit)
                {
                    _weapons.TriggerRemoteHitConfirm(true, isHeadshot);
                }
            }

            if (Runtime.Audio.AudioManager.Instance != null)
            {
                string weaponId = (_weapons != null && _weapons.ActiveWeapon != null) ? _weapons.ActiveWeapon.id : "rifle";
                Runtime.Audio.AudioManager.Instance.PlayGunshot(tracerStart, weaponId, 1.0f);
                if (IsOwner && didHit)
                {
                    Runtime.Audio.AudioManager.Instance.PlayHitMarker(isHeadshot, false);
                }
            }

            if (_visualController != null)
            {
                _visualController.HandleWeaponFired();
            }
        }

        public void ApplyServerDamage(float amount, Vector3 hitPoint, ulong attackerClientId, bool isHeadshot)
        {
            if (!IsServer) return;

            _lastAttackerClientId = attackerClientId;
            _lastHitWasHeadshot = isHeadshot;
            _health.TakeDamage(amount, hitPoint);
            SyncHealthVariablesFromServer();
            ClientTriggerHitReactionRpc();
            Debug.Log($"[ServerCombat] {NetCallsign.Value} hit by Client {attackerClientId} for {amount:F1} dmg (headshot={isHeadshot}) -> HP={_health.CurrentHealth:F1}, Armor={_health.CurrentArmor:F1}, State={_health.State}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ClientTriggerHitReactionRpc()
        {
            if (_visualController != null)
            {
                _visualController.TriggerHitReaction();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestReviveRpc(ulong targetClientId)
        {
            if (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived)
            {
                _health.TryProgressRevive(Time.deltaTime);
                SyncHealthVariablesFromServer();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestNearbyAllyReviveRpc()
        {
            if (NetworkMatchManager.Instance == null || _health.State != PlayerLifeState.Alive) return;

            foreach (var other in NetworkMatchManager.Instance.AllPlayers)
            {
                if (other == null || other == this) continue;
                if (other.NetFaction.Value != NetFaction.Value) continue;
                if (other._health.State != PlayerLifeState.Downed && other._health.State != PlayerLifeState.BeingRevived) continue;

                if (Vector3.Distance(transform.position, other.transform.position) <= 3.2f)
                {
                    other._health.TryProgressRevive(Time.deltaTime);
                    other.SyncHealthVariablesFromServer();
                    break;
                }
            }
        }

        [Rpc(SendTo.Owner)]
        private void TeleportOwnerRpc(Vector3 pos, Quaternion rot)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            if (cc != null) cc.enabled = true;
        }

        private void HandleServerDowned()
        {
            SyncHealthVariablesFromServer();
            NetworkMatchManager.Instance?.ReportDownedServer(OwnerClientId, _lastAttackerClientId);

            var veh = GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>();
            if (veh != null)
            {
                veh.ServerHandleDriverIncapacitated(OwnerClientId);
            }
        }

        private void HandleServerEliminated()
        {
            NetDeaths.Value++;
            SyncHealthVariablesFromServer();
            NetworkMatchManager.Instance?.ReportEliminationServer(OwnerClientId, _lastAttackerClientId, _lastHitWasHeadshot);

            var veh = GetComponentInParent<Runtime.Vehicles.NetworkVehicleController>();
            if (veh != null)
            {
                veh.ServerHandleDriverIncapacitated(OwnerClientId);
            }
        }

        private void HandleServerRevived()
        {
            SyncHealthVariablesFromServer();
            if (IsServer)
            {
                TeleportOwnerRpc(transform.position, transform.rotation);
            }
        }

        private void SyncHealthVariablesFromServer()
        {
            if (!IsServer) return;

            NetHealth.Value = _health.CurrentHealth;
            NetArmor.Value = _health.CurrentArmor;
            NetLifeState.Value = (int)_health.State;
            NetBleedOutRemaining.Value = _health.BleedOutRemaining;
            NetReviveProgress.Value = _health.ReviveProgress;
        }

        private void OnReplicatedHealthChanged()
        {
            if (!IsServer)
            {
                _health.ApplyReplicatedState(
                    NetHealth.Value,
                    NetArmor.Value,
                    (PlayerLifeState)NetLifeState.Value,
                    NetBleedOutRemaining.Value,
                    NetReviveProgress.Value);
            }

            var lifeState = (PlayerLifeState)NetLifeState.Value;
            if (IsOwner && _motor != null)
            {
                _motor.SetDownedStance(lifeState == PlayerLifeState.Downed || lifeState == PlayerLifeState.BeingRevived);
            }

            if (_visualRoot != null)
            {
                _visualRoot.SetActive(lifeState != PlayerLifeState.Eliminated);
            }

            UpdateOverheadLabel();
        }

        private void UpdateFactionVisuals()
        {
            string op = NetOperatorId.Value.ToString().ToLower();
            Color factionColor;
            Color headColor;

            switch (op)
            {
                case "nova":
                    factionColor = new Color(0.95f, 0.65f, 0.18f); // Bright Gold
                    headColor = new Color(0.25f, 0.85f, 0.95f);
                    break;
                case "medic":
                    factionColor = new Color(0.28f, 0.75f, 0.48f); // Field Medic Emerald
                    headColor = new Color(0.92f, 0.95f, 0.92f);
                    break;
                case "torque":
                    factionColor = new Color(0.72f, 0.52f, 0.18f); // Industrial Ochre
                    headColor = new Color(0.45f, 0.48f, 0.52f);
                    break;
                case "shade":
                    factionColor = new Color(0.18f, 0.18f, 0.22f); // Midnight Smoke
                    headColor = new Color(0.45f, 0.25f, 0.65f);
                    break;
                case "titan":
                    factionColor = new Color(0.18f, 0.45f, 0.78f); // Heavy Cobalt
                    headColor = new Color(0.45f, 0.75f, 0.98f);
                    break;
                case "trace":
                    factionColor = new Color(0.22f, 0.55f, 0.85f); // Cerulean Recon
                    headColor = new Color(0.95f, 0.8f, 0.25f);
                    break;
                case "viper":
                    factionColor = new Color(0.12f, 0.28f, 0.42f); // Dark Teal
                    headColor = new Color(0.35f, 0.85f, 0.35f);
                    break;
                case "circuit":
                    factionColor = new Color(0.35f, 0.42f, 0.5f);  // Gunmetal Copper
                    headColor = new Color(0.85f, 0.55f, 0.25f);
                    break;
                case "falcon":
                    factionColor = new Color(0.32f, 0.65f, 0.92f); // Sky Cyan
                    headColor = new Color(0.85f, 0.95f, 1f);
                    break;
                default: // REX
                    factionColor = new Color(0.85f, 0.42f, 0.15f); // Rust Amber
                    headColor = new Color(0.95f, 0.65f, 0.25f);
                    break;
            }

            if (_torsoRenderer != null && _torsoRenderer.material != null)
            {
                _torsoRenderer.material.color = factionColor;
            }

            if (_headRenderer != null && _headRenderer.material != null)
            {
                _headRenderer.material.color = headColor;
            }

            if (IsOwner && _hud != null)
            {
                string role = IsHost ? "HOST" : "CLIENT";
                string faction = NetFaction.Value == 0 ? "ROGUE" : "SENTINEL";
                string opName = NetOperatorId.Value.ToString().ToUpper();
                _hud.SetNetworkStatus($"[{role} | {NetCallsign.Value} ({opName}) | {faction}]");
            }
        }

        private void UpdateOverheadLabel()
        {
            if (_overheadLabel == null) return;
            string opName = NetOperatorId.Value.ToString().ToUpper();
            string factionTag = NetFaction.Value == 0 ? "ROGUE" : "SENTINEL";
            _overheadLabel.text = $"[{opName}] {NetCallsign.Value}\nHP: {Mathf.CeilToInt(NetHealth.Value)}";
            _overheadLabel.color = NetFaction.Value == 0 ? new Color(1f, 0.6f, 0.25f) : new Color(0.4f, 0.75f, 1f);
        }

        private void UpdateLocalBodyVisibility()
        {
            if (!IsOwner || _cameraRig == null) return;
            if (_cameraRig.Perspective == _lastAppliedPerspective) return;

            _lastAppliedPerspective = _cameraRig.Perspective;
            bool isFps = _cameraRig.Perspective == CameraPerspective.FirstPerson;

            if (_visualController != null)
            {
                _visualController.SetPerspective(_cameraRig.Perspective);
            }

            foreach (var r in _bodyRenderers)
            {
                if (r != null)
                {
                    r.shadowCastingMode = isFps ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                }
            }

            if (_weapons != null)
            {
                _weapons.SetGunRigVisible(isFps);
            }
        }

        [Rpc(SendTo.Owner)]
        private void CorrectPositionClientRpc(Vector3 correctedPosition)
        {
            if (_motor != null)
            {
                _motor.Teleport(correctedPosition);
            }
            else
            {
                transform.position = correctedPosition;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void HeartbeatServerRpc(uint sequenceId, float clientTime)
        {
            if (!_packetTracker.ValidateAndAccept(sequenceId))
            {
                _antiCheatFlags++;
                Debug.LogWarning($"[AntiCheat:ReplayDetected] Client {OwnerClientId} sent duplicate or expired packet sequence {sequenceId}. Rejected.");
            }
        }
    }
}
