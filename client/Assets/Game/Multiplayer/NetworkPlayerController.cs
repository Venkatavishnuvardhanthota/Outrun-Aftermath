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
        public NetworkVariable<int> NetLifeState = new NetworkVariable<int>((int)PlayerLifeState.Alive);
        public NetworkVariable<float> NetBleedOutRemaining = new NetworkVariable<float>(30f);
        public NetworkVariable<float> NetReviveProgress = new NetworkVariable<float>(0f);
        public NetworkVariable<int> NetKills = new NetworkVariable<int>(0);
        public NetworkVariable<int> NetDeaths = new NetworkVariable<int>(0);

        private CharacterMotor _motor;
        private PlayerHealth _health;
        private WeaponSystem _weapons;
        private OperatorAbilitySystem _abilitySystem;
        private CameraRig _cameraRig;
        private TacticalHUD _hud;

        private GameObject _visualRoot;
        private readonly List<Renderer> _bodyRenderers = new List<Renderer>();
        private Renderer _torsoRenderer;
        private Renderer _headRenderer;
        private TextMesh _overheadLabel;

        // Server-side validation state
        private float _serverNextFireTime;
        private Vector3 _lastServerValidatedPos;
        private ulong _lastAttackerClientId = ulong.MaxValue;
        private bool _lastHitWasHeadshot;
        private int _antiCheatFlags;
        private CameraPerspective _lastAppliedPerspective = (CameraPerspective)(-1);

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

            var existing = transform.Find("OperatorVisualRig");
            if (existing != null)
            {
                _visualRoot = existing.gameObject;
                _torsoRenderer = existing.Find("Body_Torso")?.GetComponent<Renderer>();
                _headRenderer = existing.Find("Head_Crit")?.GetComponent<Renderer>();
                _overheadLabel = existing.Find("OverheadNameplate")?.GetComponent<TextMesh>();
                _bodyRenderers.Clear();
                foreach (var r in _visualRoot.GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponent<TextMesh>() == null) _bodyRenderers.Add(r);
                }
                return;
            }

            _visualRoot = new GameObject("OperatorVisualRig");
            _visualRoot.transform.SetParent(transform, false);

            // Body Capsule (matches CharacterController hitbox)
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Body_Torso";
            torso.transform.SetParent(_visualRoot.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            torso.transform.localScale = new Vector3(0.7f, 0.72f, 0.7f);
            _torsoRenderer = torso.GetComponent<Renderer>();
            _bodyRenderers.Add(_torsoRenderer);

            // Head Sphere (real headshot collider!)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head_Crit";
            head.transform.SetParent(_visualRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.68f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            _headRenderer = head.GetComponent<Renderer>();
            _bodyRenderers.Add(_headRenderer);

            // Shoulder Weapon Silhouette
            var weaponVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponVis.name = "WorldWeaponProp";
            weaponVis.transform.SetParent(_visualRoot.transform, false);
            weaponVis.transform.localPosition = new Vector3(0.32f, 1.28f, 0.35f);
            weaponVis.transform.localScale = new Vector3(0.1f, 0.14f, 0.55f);
            var wCol = weaponVis.GetComponent<Collider>();
            if (wCol != null) Destroy(wCol);
            var wRenderer = weaponVis.GetComponent<Renderer>();
            if (wRenderer != null)
            {
                if (wRenderer.material != null) wRenderer.material.color = new Color(0.15f, 0.16f, 0.18f);
                _bodyRenderers.Add(wRenderer);
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

            // Subscribe to state replication
            NetFaction.OnValueChanged += (_, _) => UpdateFactionVisuals();
            NetCallsign.OnValueChanged += (_, _) => UpdateOverheadLabel();
            NetOperatorId.OnValueChanged += (_, newId) =>
            {
                if (_abilitySystem != null) _abilitySystem.SetOperator(newId.ToString());
                UpdateFactionVisuals();
                UpdateOverheadLabel();
            };
            NetHealth.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetArmor.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetLifeState.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetBleedOutRemaining.OnValueChanged += (_, _) => OnReplicatedHealthChanged();
            NetReviveProgress.OnValueChanged += (_, _) => OnReplicatedHealthChanged();

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
        }

        private void ServerTickValidation()
        {
            // Keep NetBleedOutRemaining / ReviveProgress synced when downed
            if (_health.State == PlayerLifeState.Downed || _health.State == PlayerLifeState.BeingRevived)
            {
                NetBleedOutRemaining.Value = _health.BleedOutRemaining;
                NetReviveProgress.Value = _health.ReviveProgress;
            }

            // Anti-cheat movement displacement monitor
            float maxDistPerSec = 14.0f; // Sprint/slide + latency tolerance
            float allowedStep = maxDistPerSec * Mathf.Max(Time.deltaTime, 0.02f) + 2.5f;
            float dist = Vector3.Distance(transform.position, _lastServerValidatedPos);
            if (dist > allowedStep && _health.State != PlayerLifeState.Eliminated)
            {
                _antiCheatFlags++;
                if (_antiCheatFlags % 5 == 1)
                {
                    Debug.LogWarning($"[AntiCheat] High displacement ({dist:F2}m) detected for client {OwnerClientId}");
                }
            }
            _lastServerValidatedPos = transform.position;
        }

        private void HandleLocalFireRequest(Ray aimRay, float spreadDeg, int slotIndex)
        {
            FireWeaponRpc(aimRay.origin, aimRay.direction, spreadDeg, slotIndex);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void FireWeaponRpc(Vector3 origin, Vector3 direction, float clientSpreadDeg, int weaponSlot)
        {
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

            PlayShotEffectsRpc(_weapons.MuzzleWorldPosition, firstTracerEnd, anyHit, anyHeadshot);
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
        private void PlayShotEffectsRpc(Vector3 tracerStart, Vector3 tracerEnd, bool didHit, bool isHeadshot)
        {
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
        }

        public void ApplyServerDamage(float amount, Vector3 hitPoint, ulong attackerClientId, bool isHeadshot)
        {
            if (!IsServer) return;

            _lastAttackerClientId = attackerClientId;
            _lastHitWasHeadshot = isHeadshot;
            _health.TakeDamage(amount, hitPoint);
            SyncHealthVariablesFromServer();
            Debug.Log($"[ServerCombat] {NetCallsign.Value} hit by Client {attackerClientId} for {amount:F1} dmg (headshot={isHeadshot}) -> HP={_health.CurrentHealth:F1}, Armor={_health.CurrentArmor:F1}, State={_health.State}");
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
    }
}
