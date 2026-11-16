using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Vehicles;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.UI;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Multiplayer;

namespace OutrunAftermath.Client.Runtime.Vehicles
{
    /// <summary>
    /// Server-authoritative Networked Vehicle Controller for OUTRUN: AFTERMATH.
    /// Implements vehicle physics, component health (Hull, Engine, Tires, Turret),
    /// nitro boost, 4-crew seat occupancy, manual switching, and automatic takeover.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkVehicleController : NetworkBehaviour, IDamageable
    {
        [Header("Vehicle Identification")]
        public NetworkVariable<FixedString32Bytes> NetVehicleId = new NetworkVariable<FixedString32Bytes>("raptor");
        public NetworkVariable<int> NetFaction = new NetworkVariable<int>(0); // 0 = Rogue, 1 = Sentinel
        public NetworkVariable<bool> NetIsInactive = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> NetIsExploded = new NetworkVariable<bool>(false);

        [Header("Component Health Variables")]
        public NetworkVariable<float> NetBodyHealth = new NetworkVariable<float>(900f);
        public NetworkVariable<float> NetEngineHealth = new NetworkVariable<float>(220f);
        public NetworkVariable<float> NetTireHealth = new NetworkVariable<float>(120f);
        public NetworkVariable<float> NetMountHealth = new NetworkVariable<float>(100f);

        [Header("Telemetry")]
        public NetworkVariable<float> NetSpeedMps = new NetworkVariable<float>(0f);
        public NetworkVariable<float> NetNitroFuel = new NetworkVariable<float>(100f);
        public NetworkVariable<bool> NetIsNitroActive = new NetworkVariable<bool>(false);

        [Header("Seat Occupancy (ClientId or ulong.MaxValue)")]
        public NetworkVariable<ulong> SeatDriverClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public NetworkVariable<ulong> SeatAttackerClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public NetworkVariable<ulong> SeatSupportClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public NetworkVariable<ulong> SeatHeavyClientId = new NetworkVariable<ulong>(ulong.MaxValue);

        private Rigidbody _rb;
        private VehicleDefinition _definition;

        // Visual transforms
        private GameObject _visualRoot;
        private Transform _turretMountTransform;
        private Transform _turretBarrelTransform;
        private Transform[] _wheelTransforms = new Transform[4]; // FL, FR, RL, RR

        // Local driving inputs
        private float _currentSteer;
        private float _currentThrottle;
        private bool _isHandbrake;
        private bool _isNitro;
        private float _turretYaw;
        private float _turretPitch;
        private float _nextTurretFireTime;
        private float _nextRocketFireTime;

        // Local passenger state
        private bool _isLocalPlayerInside;
        private VehicleSeat _localSeat = (VehicleSeat)(-1);
        private Camera _vehicleCamera;
        private GameObject _vehicleCamRoot;
        private Audio.VehicleAudioEmitter _audioEmitter;

        // Constants & tuning
        private const float MaxSteerAngle = 32f;
        private const float NitroBoostMultiplier = 1.45f;
        private const float NitroConsumptionRate = 35f; // % per second
        private const float NitroRechargeRate = 12f;   // % per second

        public VehicleDefinition Definition => _definition ??= VehicleCatalog.Get(NetVehicleId.Value.ToString());
        public bool IsLocalPlayerDriving => _isLocalPlayerInside && _localSeat == VehicleSeat.Driver;
        public bool IsLocalPlayerInTurret => _isLocalPlayerInside && _localSeat == VehicleSeat.Attacker;
        public bool IsLocalPlayerSupport => _isLocalPlayerInside && _localSeat == VehicleSeat.Support;
        public bool IsLocalPlayerHeavy => _isLocalPlayerInside && _localSeat == VehicleSeat.HeavyRecon;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 1850f;
            _rb.linearDamping = 0.35f;
            _rb.angularDamping = 2.5f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Lower center of mass to prevent unwanted rollovers during sharp drifting
            _rb.centerOfMass = new Vector3(0f, -0.45f, 0.1f);

            EnsureVehicleVisuals();
        }

        public override void OnNetworkSpawn()
        {
            _definition = VehicleCatalog.Get(NetVehicleId.Value.ToString());

            if (IsServer)
            {
                NetBodyHealth.Value = _definition.BodyHealth;
                NetEngineHealth.Value = _definition.EngineHealth;
                NetTireHealth.Value = _definition.TireHealth;
                NetMountHealth.Value = 100f;
                NetNitroFuel.Value = 100f;
            }

            NetBodyHealth.OnValueChanged += OnHealthChanged;
            NetIsExploded.OnValueChanged += OnExplodedChanged;
            SeatDriverClientId.OnValueChanged += OnSeatAssignmentChanged;
            SeatAttackerClientId.OnValueChanged += OnSeatAssignmentChanged;
            SeatSupportClientId.OnValueChanged += OnSeatAssignmentChanged;
            SeatHeavyClientId.OnValueChanged += OnSeatAssignmentChanged;

            if (Audio.AudioManager.Instance != null)
            {
                _audioEmitter = Audio.AudioManager.Instance.AttachVehicleAudio(gameObject);
            }

            UpdateLocalOccupancyStatus();
        }

        public override void OnNetworkDespawn()
        {
            NetBodyHealth.OnValueChanged -= OnHealthChanged;
            NetIsExploded.OnValueChanged -= OnExplodedChanged;
            SeatDriverClientId.OnValueChanged -= OnSeatAssignmentChanged;
            SeatAttackerClientId.OnValueChanged -= OnSeatAssignmentChanged;
            SeatSupportClientId.OnValueChanged -= OnSeatAssignmentChanged;
            SeatHeavyClientId.OnValueChanged -= OnSeatAssignmentChanged;

            if (_isLocalPlayerInside)
            {
                DismountLocalPlayer(immediate: true);
            }
        }

        private void Update()
        {
            if (!IsSpawned || NetIsExploded.Value) return;

            if (_audioEmitter != null)
            {
                _audioEmitter.UpdateTelemetry(NetSpeedMps.Value, NetIsNitroActive.Value, _isHandbrake);
            }

            if (_isLocalPlayerInside)
            {
                LocalCrewUpdate();
            }

            // Client visual wheels rotation
            float speed = NetSpeedMps.Value;
            float wheelSpin = speed * 120f * Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                if (_wheelTransforms[i] != null)
                {
                    _wheelTransforms[i].Rotate(Vector3.right, wheelSpin, Space.Self);
                }
            }

            // Steer visual front wheels
            if (_wheelTransforms[0] != null && _wheelTransforms[1] != null)
            {
                float steerDeg = _currentSteer * MaxSteerAngle;
                _wheelTransforms[0].localRotation = Quaternion.Euler(0f, steerDeg, 0f);
                _wheelTransforms[1].localRotation = Quaternion.Euler(0f, steerDeg, 0f);
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || NetIsExploded.Value || NetIsInactive.Value) return;

            if (IsServer)
            {
                ServerVehiclePhysicsTick();
            }
        }

        #region Driving & Physics (Server-Authoritative)

        private void ServerVehiclePhysicsTick()
        {
            // Calculate health multipliers from simulation
            float engineMult = NetEngineHealth.Value <= 0f ? 0f : (NetEngineHealth.Value < _definition.EngineHealth * 0.3f ? 0.45f : 1f);
            float gripMult = NetTireHealth.Value <= 0f ? 0.3f : (NetTireHealth.Value < _definition.TireHealth * 0.35f ? 0.65f : 1f);

            bool driverPresent = SeatDriverClientId.Value != ulong.MaxValue;
            if (!driverPresent)
            {
                _currentThrottle = 0f;
                _currentSteer = 0f;
                _isNitro = false;
            }

            // Nitro consumption & recharge
            if (_isNitro && _currentThrottle > 0.1f && NetNitroFuel.Value > 0f)
            {
                NetNitroFuel.Value = Mathf.Max(0f, NetNitroFuel.Value - NitroConsumptionRate * Time.fixedDeltaTime);
                NetIsNitroActive.Value = true;
            }
            else
            {
                NetIsNitroActive.Value = false;
                if (!_isNitro && NetNitroFuel.Value < 100f)
                {
                    NetNitroFuel.Value = Mathf.Min(100f, NetNitroFuel.Value + NitroRechargeRate * Time.fixedDeltaTime);
                }
            }

            float effectiveAccel = _definition.Acceleration * engineMult;
            if (NetIsNitroActive.Value) effectiveAccel *= NitroBoostMultiplier;

            float maxSpeed = _definition.MaxSpeedMps * engineMult;
            if (NetIsNitroActive.Value) maxSpeed *= 1.25f;

            // Forward / Reverse drive force
            Vector3 forward = transform.forward;
            float currentFwdSpeed = Vector3.Dot(_rb.linearVelocity, forward);

            if (Mathf.Abs(_currentThrottle) > 0.05f)
            {
                if ((_currentThrottle > 0 && currentFwdSpeed < maxSpeed) ||
                    (_currentThrottle < 0 && currentFwdSpeed > -maxSpeed * 0.4f))
                {
                    Vector3 driveForce = forward * (_currentThrottle * effectiveAccel * _rb.mass * 1.1f);
                    _rb.AddForce(driveForce, ForceMode.Force);
                }
            }

            // Steering & Drift
            if (Mathf.Abs(_currentSteer) > 0.05f && Mathf.Abs(currentFwdSpeed) > 0.5f)
            {
                float turnDir = Mathf.Sign(currentFwdSpeed) * _currentSteer;
                float turnSpeed = 45f * gripMult * (_isHandbrake ? 1.6f : 1.0f);
                Quaternion deltaRot = Quaternion.Euler(0f, turnDir * turnSpeed * Time.fixedDeltaTime, 0f);
                _rb.MoveRotation(_rb.rotation * deltaRot);
            }

            // Lateral friction / counter slide
            Vector3 right = transform.right;
            float lateralSpeed = Vector3.Dot(_rb.linearVelocity, right);
            float lateralFriction = _isHandbrake ? 0.35f : (2.8f * gripMult);
            _rb.AddForce(-right * (lateralSpeed * lateralFriction * _rb.mass), ForceMode.Force);

            // Update telemetry
            NetSpeedMps.Value = _rb.linearVelocity.magnitude;
        }

        [Rpc(SendTo.Server)]
        public void SendDriveInputRpc(float throttle, float steer, bool handbrake, bool nitro)
        {
            if (SeatDriverClientId.Value != ulong.MaxValue)
            {
                _currentThrottle = Mathf.Clamp(throttle, -1f, 1f);
                _currentSteer = Mathf.Clamp(steer, -1f, 1f);
                _isHandbrake = handbrake;
                _isNitro = nitro;
            }
        }

        #endregion

        #region Seat Management & Takeover

        public ulong GetSeatOccupant(VehicleSeat seat)
        {
            return seat switch
            {
                VehicleSeat.Driver => SeatDriverClientId.Value,
                VehicleSeat.Attacker => SeatAttackerClientId.Value,
                VehicleSeat.Support => SeatSupportClientId.Value,
                VehicleSeat.HeavyRecon => SeatHeavyClientId.Value,
                _ => ulong.MaxValue
            };
        }

        [Rpc(SendTo.Server)]
        public void RequestBoardVehicleRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            BoardPlayerServer(senderId);
        }

        public bool BoardPlayerServer(ulong clientId)
        {
            if (!IsServer) return false;
            if (NetIsExploded.Value) return false;

            // Check if player is already inside
            if (IsPlayerInAnySeat(clientId)) return false;

            // Prioritize driver seat if vacant, else attacker, support, heavy
            int assignedSeat = -1;
            if (SeatDriverClientId.Value == ulong.MaxValue)
            {
                SeatDriverClientId.Value = clientId;
                assignedSeat = (int)VehicleSeat.Driver;
            }
            else if (SeatAttackerClientId.Value == ulong.MaxValue)
            {
                SeatAttackerClientId.Value = clientId;
                assignedSeat = (int)VehicleSeat.Attacker;
            }
            else if (SeatSupportClientId.Value == ulong.MaxValue)
            {
                SeatSupportClientId.Value = clientId;
                assignedSeat = (int)VehicleSeat.Support;
            }
            else if (SeatHeavyClientId.Value == ulong.MaxValue)
            {
                SeatHeavyClientId.Value = clientId;
                assignedSeat = (int)VehicleSeat.HeavyRecon;
            }
            else
            {
                // Vehicle full
                return false;
            }

            NotifyBoardSuccessClientRpc(clientId, assignedSeat);

            // Sync faction if vehicle was unassigned
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var pc = client.PlayerObject?.GetComponent<NetworkPlayerController>();
                if (pc != null)
                {
                    NetFaction.Value = pc.NetFaction.Value;
                }
            }

            CheckCrewActivityServer();
            Debug.Log($"[Convoy] Client {clientId} boarded {NetVehicleId.Value} in seat {(VehicleSeat)assignedSeat}");
            return true;
        }

        [Rpc(SendTo.Server)]
        public void RequestDismountRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            ClearSeatForPlayer(senderId);
            NotifyDismountClientRpc(senderId);
            CheckCrewActivityServer();
        }

        [Rpc(SendTo.Server)]
        public void RequestSwitchSeatRpc(int targetSeatInt, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            VehicleSeat targetSeat = (VehicleSeat)targetSeatInt;

            if (GetSeatOccupant(targetSeat) != ulong.MaxValue)
            {
                // Target seat occupied
                return;
            }

            ClearSeatForPlayer(senderId);

            switch (targetSeat)
            {
                case VehicleSeat.Driver: SeatDriverClientId.Value = senderId; break;
                case VehicleSeat.Attacker: SeatAttackerClientId.Value = senderId; break;
                case VehicleSeat.Support: SeatSupportClientId.Value = senderId; break;
                case VehicleSeat.HeavyRecon: SeatHeavyClientId.Value = senderId; break;
            }

            NotifyBoardSuccessClientRpc(senderId, targetSeatInt);
        }

        /// <summary>
        /// Server-side automatic takeover when the driver is eliminated or incapacitated.
        /// Smoothly promotes the next available passenger to the Driver seat.
        /// </summary>
        public void ServerHandleDriverIncapacitated(ulong driverClientId)
        {
            if (!IsServer) return;
            if (SeatDriverClientId.Value != driverClientId) return;

            Debug.Log($"[Convoy] Driver {driverClientId} incapacitated! Initiating automatic takeover...");

            ulong promotedClientId = ulong.MaxValue;
            VehicleSeat previousSeat = (VehicleSeat)(-1);

            if (SeatAttackerClientId.Value != ulong.MaxValue)
            {
                promotedClientId = SeatAttackerClientId.Value;
                previousSeat = VehicleSeat.Attacker;
                SeatAttackerClientId.Value = driverClientId; // Swap incapacitated driver into passenger seat
            }
            else if (SeatSupportClientId.Value != ulong.MaxValue)
            {
                promotedClientId = SeatSupportClientId.Value;
                previousSeat = VehicleSeat.Support;
                SeatSupportClientId.Value = driverClientId;
            }
            else if (SeatHeavyClientId.Value != ulong.MaxValue)
            {
                promotedClientId = SeatHeavyClientId.Value;
                previousSeat = VehicleSeat.HeavyRecon;
                SeatHeavyClientId.Value = driverClientId;
            }

            if (promotedClientId != ulong.MaxValue)
            {
                SeatDriverClientId.Value = promotedClientId;
                NotifyBoardSuccessClientRpc(promotedClientId, (int)VehicleSeat.Driver);
                NotifyBoardSuccessClientRpc(driverClientId, (int)previousSeat);
                NetworkMatchManager.Instance?.BroadcastFeedRpc($"CREW TAKEOVER: PASSENGER TOOK DRIVER SEAT!");
            }
            else
            {
                // No living passengers remaining
                SeatDriverClientId.Value = ulong.MaxValue;
                _currentThrottle = 0f;
                _currentSteer = 0f;
                CheckCrewActivityServer();
            }
        }

        private bool IsPlayerInAnySeat(ulong clientId)
        {
            return SeatDriverClientId.Value == clientId ||
                   SeatAttackerClientId.Value == clientId ||
                   SeatSupportClientId.Value == clientId ||
                   SeatHeavyClientId.Value == clientId;
        }

        private void ClearSeatForPlayer(ulong clientId)
        {
            if (SeatDriverClientId.Value == clientId) SeatDriverClientId.Value = ulong.MaxValue;
            if (SeatAttackerClientId.Value == clientId) SeatAttackerClientId.Value = ulong.MaxValue;
            if (SeatSupportClientId.Value == clientId) SeatSupportClientId.Value = ulong.MaxValue;
            if (SeatHeavyClientId.Value == clientId) SeatHeavyClientId.Value = ulong.MaxValue;
        }

        private void CheckCrewActivityServer()
        {
            bool hasAnyCrew = SeatDriverClientId.Value != ulong.MaxValue ||
                              SeatAttackerClientId.Value != ulong.MaxValue ||
                              SeatSupportClientId.Value != ulong.MaxValue ||
                              SeatHeavyClientId.Value != ulong.MaxValue;

            NetIsInactive.Value = !hasAnyCrew;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyBoardSuccessClientRpc(ulong targetClientId, int seatIndex)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                _isLocalPlayerInside = true;
                _localSeat = (VehicleSeat)seatIndex;
                MountLocalPlayer();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyDismountClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                DismountLocalPlayer(immediate: false);
            }
        }

        private void MountLocalPlayer()
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                // Disable CharacterController and colliders while in vehicle
                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Parent player to vehicle
                localPlayer.transform.SetParent(transform, false);
                localPlayer.transform.localPosition = GetSeatOffset(_localSeat);
                localPlayer.transform.localRotation = Quaternion.identity;

                // Hide local player body mesh while mounted
                var rig = localPlayer.transform.Find("OperatorVisualRig");
                if (rig != null) rig.gameObject.SetActive(false);
            }

            SetupVehicleCamera();

            var hud = FindAnyObjectByType<TacticalHUD>();
            if (hud != null)
            {
                hud.SetMissionStatus($"CREW SEAT: {_localSeat.ToString().ToUpper()}");
            }

            Debug.Log($"[Vehicle] Local player boarded {_definition.DisplayName} in seat {_localSeat}");
        }

        private void DismountLocalPlayer(bool immediate)
        {
            _isLocalPlayerInside = false;
            _localSeat = (VehicleSeat)(-1);

            TeardownVehicleCamera();

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                localPlayer.transform.SetParent(null, true);
                Vector3 exitPos = transform.position + transform.right * 2.2f + Vector3.up * 0.5f;
                localPlayer.transform.position = exitPos;

                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var rig = localPlayer.transform.Find("OperatorVisualRig");
                if (rig != null) rig.gameObject.SetActive(true);
            }

            Debug.Log($"[Vehicle] Local player dismounted {_definition.DisplayName}");
        }

        private Vector3 GetSeatOffset(VehicleSeat seat)
        {
            return seat switch
            {
                VehicleSeat.Driver => new Vector3(-0.55f, 0.4f, 0.2f),
                VehicleSeat.Attacker => new Vector3(0f, 1.4f, -0.4f), // Turret hatch
                VehicleSeat.Support => new Vector3(0.55f, 0.4f, 0.2f),
                VehicleSeat.HeavyRecon => new Vector3(0f, 0.6f, -1.2f), // Flatbed
                _ => Vector3.zero
            };
        }

        #endregion

        #region Mounted Weapons & Support Systems

        private void LocalCrewUpdate()
        {
            if (InputManager.Instance == null) return;
            var input = InputManager.Instance.Current;

            // Manual seat switching hotkeys [1], [2], [3], [4]
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit1Key.wasPressedThisFrame && _localSeat != VehicleSeat.Driver) RequestSwitchSeatRpc((int)VehicleSeat.Driver);
                if (kb.digit2Key.wasPressedThisFrame && _localSeat != VehicleSeat.Attacker) RequestSwitchSeatRpc((int)VehicleSeat.Attacker);
                if (kb.digit3Key.wasPressedThisFrame && _localSeat != VehicleSeat.Support) RequestSwitchSeatRpc((int)VehicleSeat.Support);
                if (kb.digit4Key.wasPressedThisFrame && _localSeat != VehicleSeat.HeavyRecon) RequestSwitchSeatRpc((int)VehicleSeat.HeavyRecon);
            }

            // Dismount hotkey [X]
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.xKey.wasPressedThisFrame)
            {
                RequestDismountRpc();
                return;
            }

            // Driver role: send drive inputs
            if (IsLocalPlayerDriving)
            {
                float throttle = input.Move.y;
                float steer = input.Move.x;
                bool handbrake = input.Jump; // Spacebar handbrake
                bool nitro = input.Sprint;   // Shift nitro

                SendDriveInputRpc(throttle, steer, handbrake, nitro);
            }

            // Attacker role: control & fire roof-mounted heavy turret
            if (IsLocalPlayerInTurret && _turretMountTransform != null)
            {
                _turretYaw += input.Look.x * 2.2f;
                _turretPitch = Mathf.Clamp(_turretPitch - input.Look.y * 2.0f, -15f, 50f);
                _turretMountTransform.localRotation = Quaternion.Euler(0f, _turretYaw, 0f);
                if (_turretBarrelTransform != null)
                {
                    _turretBarrelTransform.localRotation = Quaternion.Euler(_turretPitch, 0f, 0f);
                }

                if (input.Fire && Time.time >= _nextTurretFireTime && NetMountHealth.Value > 0f)
                {
                    _nextTurretFireTime = Time.time + 0.12f; // 500 RPM
                    Vector3 origin = _turretBarrelTransform != null ? _turretBarrelTransform.position : _turretMountTransform.position;
                    Vector3 dir = _turretBarrelTransform != null ? _turretBarrelTransform.forward : _turretMountTransform.forward;
                    FireVehicleWeaponRpc(0, origin, dir); // 0 = Mounted Turret
                }
            }

            // Support role: repair vehicle or deploy countermeasures
            if (IsLocalPlayerSupport)
            {
                if (input.Interact && IsServer)
                {
                    ServerRepairVehicle(Time.deltaTime * 25f);
                }
                else if (input.Interact)
                {
                    RequestSupportRepairRpc(Time.deltaTime);
                }
            }

            // Heavy Recon role: fire armor-piercing heavy cannon / rocket
            if (IsLocalPlayerHeavy)
            {
                if (input.Fire && Time.time >= _nextRocketFireTime)
                {
                    _nextRocketFireTime = Time.time + 3.0f; // 3s reload
                    Vector3 origin = transform.position + transform.up * 1.5f + transform.forward * 1.5f;
                    Vector3 dir = transform.forward;
                    FireVehicleWeaponRpc(1, origin, dir); // 1 = Heavy Rocket
                }
            }
        }

        [Rpc(SendTo.Server)]
        public void FireVehicleWeaponRpc(int weaponType, Vector3 origin, Vector3 direction, RpcParams rpcParams = default)
        {
            ulong shooterId = rpcParams.Receive.SenderClientId;
            if (NetIsExploded.Value || NetIsInactive.Value) return;

            if (weaponType == 0) // Turret
            {
                if (NetMountHealth.Value <= 0f) return;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, 150f))
                {
                    var targetVehicle = hit.collider.GetComponentInParent<NetworkVehicleController>();
                    if (targetVehicle != null && targetVehicle != this)
                    {
                        // Check component hit
                        var comp = VehicleComponent.Body;
                        if (hit.collider.name.Contains("Engine")) comp = VehicleComponent.Engine;
                        else if (hit.collider.name.Contains("Wheel")) comp = VehicleComponent.Tires;
                        else if (hit.collider.name.Contains("Turret")) comp = VehicleComponent.MountedWeapon;

                        targetVehicle.ApplyVehicleDamageServer(comp, 24f, shooterId, hit.point);
                    }
                    else
                    {
                        var dmg = hit.collider.GetComponentInParent<IDamageable>();
                        dmg?.TakeDamage(28f, hit.point, direction, false);
                    }
                }

                BroadcastTracerClientRpc(origin, origin + direction * 60f);
            }
            else if (weaponType == 1) // Heavy Rocket
            {
                if (Physics.Raycast(origin, direction, out RaycastHit hit, 200f))
                {
                    Vector3 blastPos = hit.point;
                    float radius = 7.5f;
                    float blastDamage = 175f;

                    var hits = Physics.OverlapSphere(blastPos, radius);
                    foreach (var c in hits)
                    {
                        var targetVeh = c.GetComponentInParent<NetworkVehicleController>();
                        if (targetVeh != null && targetVeh != this)
                        {
                            targetVeh.ApplyVehicleDamageServer(VehicleComponent.Body, blastDamage, shooterId, blastPos);
                        }
                        else
                        {
                            var dmg = c.GetComponentInParent<IDamageable>();
                            dmg?.TakeDamage(blastDamage * 0.7f, blastPos, Vector3.up, false);
                        }
                    }
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastTracerClientRpc(Vector3 start, Vector3 end)
        {
            // Lightweight visual tracer for mounted machine gun
            StartCoroutine(DrawTracerRoutine(start, end));
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayGunshot(start, "turret", 1.15f);
            }
        }

        private IEnumerator DrawTracerRoutine(Vector3 start, Vector3 end)
        {
            var lineGo = new GameObject("TurretTracer");
            var line = lineGo.AddComponent<LineRenderer>();
            line.startWidth = 0.08f;
            line.endWidth = 0.05f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.85f, 0.2f);
                line.material = mat;
            }

            yield return new WaitForSeconds(0.06f);
            Destroy(lineGo);
        }

        [Rpc(SendTo.Server)]
        public void RequestSupportRepairRpc(float deltaTime)
        {
            ServerRepairVehicle(deltaTime * 25f);
        }

        private void ServerRepairVehicle(float amount)
        {
            if (NetIsExploded.Value) return;

            NetBodyHealth.Value = Mathf.Min(_definition.BodyHealth, NetBodyHealth.Value + amount);
            NetEngineHealth.Value = Mathf.Min(_definition.EngineHealth, NetEngineHealth.Value + amount * 0.6f);
            NetTireHealth.Value = Mathf.Min(_definition.TireHealth, NetTireHealth.Value + amount * 0.5f);
            NetMountHealth.Value = Mathf.Min(100f, NetMountHealth.Value + amount * 0.8f);
        }

        #endregion

        #region Damage & Destruction

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection, bool isHeadshot)
        {
            // Default IDamageable interface delegates to Body damage on server
            if (IsServer)
            {
                ApplyVehicleDamageServer(VehicleComponent.Body, amount, ulong.MaxValue, hitPoint);
            }
        }

        public void ApplyExplosiveDamageServer(float amount)
        {
            if (IsServer)
            {
                ApplyVehicleDamageServer(VehicleComponent.Body, amount, ulong.MaxValue, transform.position);
            }
        }

        public void ApplyVehicleDamageServer(VehicleComponent component, float amount, ulong attackerClientId, Vector3 hitPoint)
        {
            if (!IsServer || NetIsExploded.Value) return;

            switch (component)
            {
                case VehicleComponent.Body:
                    NetBodyHealth.Value = Mathf.Max(0f, NetBodyHealth.Value - amount);
                    if (NetBodyHealth.Value <= 0f)
                    {
                        TriggerServerExplosion(attackerClientId);
                    }
                    break;

                case VehicleComponent.Engine:
                    NetEngineHealth.Value = Mathf.Max(0f, NetEngineHealth.Value - amount);
                    break;

                case VehicleComponent.Tires:
                    NetTireHealth.Value = Mathf.Max(0f, NetTireHealth.Value - amount);
                    break;

                case VehicleComponent.MountedWeapon:
                    NetMountHealth.Value = Mathf.Max(0f, NetMountHealth.Value - amount);
                    break;
            }
        }

        private void TriggerServerExplosion(ulong attackerClientId)
        {
            NetIsExploded.Value = true;
            NetIsInactive.Value = true;

            // Explosion damage radius to nearby entities
            Vector3 center = transform.position;
            float blastRadius = 9.0f;
            float maxDamage = 220f;

            var cols = Physics.OverlapSphere(center, blastRadius);
            foreach (var col in cols)
            {
                if (col.transform.IsChildOf(transform)) continue;
                var pc = col.GetComponentInParent<NetworkPlayerController>();
                if (pc != null)
                {
                    pc.ApplyServerDamage(maxDamage, center, attackerClientId, false);
                }
            }

            // Eject/down all living occupants
            EjectAllCrewOnCatastrophe();

            string victimFaction = NetFaction.Value == 0 ? "ROGUE RAPTOR" : "SENTINEL INTERCEPTOR";
            NetworkMatchManager.Instance?.BroadcastFeedRpc($"{victimFaction} WAS DESTROYED!");
            NetworkMatchManager.Instance?.ReportVehicleDestroyedServer(NetFaction.Value);
        }

        private void EjectAllCrewOnCatastrophe()
        {
            ulong[] occupants = { SeatDriverClientId.Value, SeatAttackerClientId.Value, SeatSupportClientId.Value, SeatHeavyClientId.Value };
            foreach (var cid in occupants)
            {
                if (cid != ulong.MaxValue)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(cid, out var client))
                    {
                        var pc = client.PlayerObject?.GetComponent<NetworkPlayerController>();
                        if (pc != null)
                        {
                            pc.ApplyServerDamage(120f, transform.position, ulong.MaxValue, false);
                        }
                    }
                    ClearSeatForPlayer(cid);
                    NotifyDismountClientRpc(cid);
                }
            }
        }

        private void OnHealthChanged(float oldVal, float newVal)
        {
            // Visual smoke/fire threshold
            if (newVal < _definition.BodyHealth * 0.25f)
            {
                // Heavily damaged smoke effect
            }
        }

        private void OnExplodedChanged(bool oldVal, bool isExploded)
        {
            if (isExploded)
            {
                // Charred wreck visual material
                var rends = GetComponentsInChildren<Renderer>();
                foreach (var r in rends)
                {
                    if (r.material != null) r.material.color = new Color(0.12f, 0.12f, 0.12f);
                }

                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                if (Audio.AudioManager.Instance != null)
                {
                    Audio.AudioManager.Instance.PlayExplosion(transform.position, 1.8f);
                }
            }
        }

        private void OnSeatAssignmentChanged(ulong oldId, ulong newId)
        {
            UpdateLocalOccupancyStatus();
        }

        private void UpdateLocalOccupancyStatus()
        {
            if (NetworkManager.Singleton == null) return;
            ulong myId = NetworkManager.Singleton.LocalClientId;

            if (SeatDriverClientId.Value == myId) _localSeat = VehicleSeat.Driver;
            else if (SeatAttackerClientId.Value == myId) _localSeat = VehicleSeat.Attacker;
            else if (SeatSupportClientId.Value == myId) _localSeat = VehicleSeat.Support;
            else if (SeatHeavyClientId.Value == myId) _localSeat = VehicleSeat.HeavyRecon;
            else
            {
                if (_isLocalPlayerInside) DismountLocalPlayer(immediate: true);
                return;
            }

            _isLocalPlayerInside = true;
        }

        #endregion

        #region Camera Rig for Vehicle

        private void SetupVehicleCamera()
        {
            if (_vehicleCamRoot != null) return;

            _vehicleCamRoot = new GameObject("VehicleCameraRig");
            _vehicleCamRoot.transform.SetParent(transform, false);

            _vehicleCamera = _vehicleCamRoot.AddComponent<Camera>();
            _vehicleCamera.fieldOfView = 68f;
            _vehicleCamera.nearClipPlane = 0.3f;
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                _vehicleCamRoot.AddComponent<AudioListener>();
            }

            if (_localSeat == VehicleSeat.Driver)
            {
                // Third-person chase camera
                _vehicleCamRoot.transform.localPosition = new Vector3(0f, 2.6f, -5.2f);
                _vehicleCamRoot.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            }
            else if (_localSeat == VehicleSeat.Attacker)
            {
                // Turret first-person aiming camera
                _vehicleCamRoot.transform.localPosition = new Vector3(0f, 1.85f, -0.4f);
                _vehicleCamRoot.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Passenger tactical camera
                _vehicleCamRoot.transform.localPosition = new Vector3(0f, 2.2f, -3.8f);
                _vehicleCamRoot.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            }
        }

        private void TeardownVehicleCamera()
        {
            if (_vehicleCamRoot != null)
            {
                Destroy(_vehicleCamRoot);
                _vehicleCamRoot = null;
                _vehicleCamera = null;
            }
        }

        #endregion

        #region Procedural Visual Hierarchy

        private void EnsureVehicleVisuals()
        {
            if (_visualRoot != null) return;

            _visualRoot = new GameObject("WastelandRaptor_VisualRig");
            _visualRoot.transform.SetParent(transform, false);

            var bodyMat = CreateLitMaterial(new Color(0.28f, 0.32f, 0.36f), 0.5f);
            var darkMetal = CreateLitMaterial(new Color(0.15f, 0.15f, 0.16f), 0.7f);
            var rubberMat = CreateLitMaterial(new Color(0.08f, 0.08f, 0.08f), 0.1f);
            var orangeAccent = CreateLitMaterial(new Color(0.85f, 0.45f, 0.1f), 0.4f);

            // 1. Lower Chassis
            var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Vehicle_Chassis";
            chassis.transform.SetParent(_visualRoot.transform, false);
            chassis.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            chassis.transform.localScale = new Vector3(2.1f, 0.45f, 4.4f);
            if (darkMetal != null) chassis.GetComponent<Renderer>().sharedMaterial = darkMetal;

            // 2. Cabin (Windows / Crew compartment)
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Vehicle_Cabin";
            cabin.transform.SetParent(_visualRoot.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 1.15f, 0.2f);
            cabin.transform.localScale = new Vector3(1.85f, 0.85f, 2.2f);
            if (bodyMat != null) cabin.GetComponent<Renderer>().sharedMaterial = bodyMat;

            // 3. Engine Hood (Critical hitbox)
            var hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hood.name = "Component_Engine_Hood";
            hood.transform.SetParent(_visualRoot.transform, false);
            hood.transform.localPosition = new Vector3(0f, 0.85f, 1.75f);
            hood.transform.localScale = new Vector3(1.8f, 0.45f, 1.25f);
            if (orangeAccent != null) hood.GetComponent<Renderer>().sharedMaterial = orangeAccent;

            // 4. Flatbed (Rear cargo & Heavy mount)
            var flatbed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flatbed.name = "Vehicle_Flatbed";
            flatbed.transform.SetParent(_visualRoot.transform, false);
            flatbed.transform.localPosition = new Vector3(0f, 0.75f, -1.45f);
            flatbed.transform.localScale = new Vector3(1.9f, 0.35f, 1.5f);
            if (darkMetal != null) flatbed.GetComponent<Renderer>().sharedMaterial = darkMetal;

            // 5. Heavy Turret Mount
            var turretMount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turretMount.name = "Component_Turret_Mount";
            turretMount.transform.SetParent(_visualRoot.transform, false);
            turretMount.transform.localPosition = new Vector3(0f, 1.7f, 0.1f);
            turretMount.transform.localScale = new Vector3(0.7f, 0.25f, 0.7f);
            if (darkMetal != null) turretMount.GetComponent<Renderer>().sharedMaterial = darkMetal;
            _turretMountTransform = turretMount.transform;

            // Turret Barrel
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Turret_Barrel";
            barrel.transform.SetParent(_turretMountTransform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.25f, 0.8f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.12f, 0.7f, 0.12f);
            if (orangeAccent != null) barrel.GetComponent<Renderer>().sharedMaterial = orangeAccent;
            _turretBarrelTransform = barrel.transform;

            // 6. Wheels (FL, FR, RL, RR)
            Vector3[] wheelPositions = new[]
            {
                new Vector3(-1.15f, 0.45f, 1.45f),  // FL
                new Vector3(1.15f, 0.45f, 1.45f),   // FR
                new Vector3(-1.15f, 0.45f, -1.35f), // RL
                new Vector3(1.15f, 0.45f, -1.35f)   // RR
            };

            for (int i = 0; i < 4; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Component_Wheel_{i}";
                wheel.transform.SetParent(_visualRoot.transform, false);
                wheel.transform.localPosition = wheelPositions[i];
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);
                if (rubberMat != null) wheel.GetComponent<Renderer>().sharedMaterial = rubberMat;
                _wheelTransforms[i] = wheel.transform;
            }

            // Ensure colliders are enabled on body, engine, wheels for raycast hit registration
            var col = gameObject.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.95f, 0f);
                col.size = new Vector3(2.3f, 1.8f, 4.8f);
            }
        }

        private static Material CreateLitMaterial(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color");

            if (shader == null) return null;

            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        #endregion
    }
}
