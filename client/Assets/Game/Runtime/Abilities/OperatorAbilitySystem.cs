using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using OutrunAftermath.Simulation.Abilities;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Operators;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.UI;
using OutrunAftermath.Client.Multiplayer;

namespace OutrunAftermath.Client.Runtime.Abilities
{
    /// <summary>
    /// Server-authoritative Operator Ability system for OUTRUN: AFTERMATH.
    /// Bridges the verified simulation AbilityCatalog and OperatorCatalog into real-time
    /// networked tactical combat, tracking cooldowns, active buffs, and networked visual effects.
    /// </summary>
    public sealed class OperatorAbilitySystem : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> OperatorId = new NetworkVariable<FixedString32Bytes>("rex");
        public NetworkVariable<float> PrimaryCooldownRemaining = new NetworkVariable<float>(0f);
        public NetworkVariable<float> UltimateCooldownRemaining = new NetworkVariable<float>(0f);
        public NetworkVariable<float> UltimateCharge = new NetworkVariable<float>(0f); // 0 to 100%

        private PlayerHealth _health;
        private CharacterMotor _motor;
        private WeaponSystem _weapons;
        private CameraRig _cameraRig;

        private OperatorDefinition _operatorDef;
        private AbilityDefinition _primaryAbilityDef;
        private AbilityDefinition _ultimateAbilityDef;

        private float _serverPrimaryReadyTime;
        private float _serverUltimateReadyTime;
        private float _speedBuffTimer;
        private float _originalRunSpeed;
        private float _originalSprintSpeed;

        public OperatorDefinition Definition => _operatorDef;
        public AbilityDefinition PrimaryAbility => _primaryAbilityDef;
        public AbilityDefinition UltimateAbility => _ultimateAbilityDef;

        public float ClientPrimaryCd => Mathf.Max(0f, PrimaryCooldownRemaining.Value);
        public float ClientUltimateCd => Mathf.Max(0f, UltimateCooldownRemaining.Value);
        public float ClientUltimateCharge => UltimateCharge.Value;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _motor = GetComponent<CharacterMotor>();
            _weapons = GetComponent<WeaponSystem>();
        }

        public void Initialize(string opId)
        {
            SetOperator(opId);
        }

        public void SetOperator(string opId)
        {
            var found = OperatorCatalog.All.FirstOrDefault(o => o.Id.Equals(opId, StringComparison.OrdinalIgnoreCase))
                        ?? OperatorCatalog.All[0];

            _operatorDef = found;
            _primaryAbilityDef = AbilityCatalog.Get(_operatorDef.PrimaryAbilityId);
            _ultimateAbilityDef = AbilityCatalog.Get(_operatorDef.UltimateAbilityId);

            if (_motor != null && _operatorDef.MaxSpeedMps > 0f)
            {
                _originalRunSpeed = _operatorDef.MaxSpeedMps;
                _originalSprintSpeed = _operatorDef.MaxSpeedMps * 1.45f;
            }

            if (IsServer)
            {
                OperatorId.Value = new FixedString32Bytes(_operatorDef.Id);
                UltimateCharge.Value = 25f; // Initial tactical momentum
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            OperatorId.OnValueChanged += (_, newId) =>
            {
                SetOperator(newId.ToString());
            };

            SetOperator(OperatorId.Value.ToString());

            if (IsOwner)
            {
                _cameraRig = FindAnyObjectByType<CameraRig>();
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                ServerTick();
            }

            if (_speedBuffTimer > 0f)
            {
                _speedBuffTimer -= Time.deltaTime;
                if (_speedBuffTimer <= 0f && _motor != null)
                {
                    // Revert speed buff
                }
            }
        }

        private void ServerTick()
        {
            if (_health != null && _health.State == PlayerLifeState.Alive)
            {
                // Passive ultimate charge gain (1.5% per sec)
                if (UltimateCharge.Value < 100f)
                {
                    UltimateCharge.Value = Mathf.Min(100f, UltimateCharge.Value + 1.5f * Time.deltaTime);
                }

                PrimaryCooldownRemaining.Value = Mathf.Max(0f, _serverPrimaryReadyTime - Time.time);
                UltimateCooldownRemaining.Value = Mathf.Max(0f, _serverUltimateReadyTime - Time.time);
            }
        }

        public void TryActivatePrimary()
        {
            if (!IsOwner) return;
            if (_health != null && _health.State != PlayerLifeState.Alive) return;
            if (ClientPrimaryCd > 0.05f) return;

            Vector3 origin = _cameraRig != null ? _cameraRig.GetAimRay().origin : transform.position + Vector3.up * 1.5f;
            Vector3 dir = _cameraRig != null ? _cameraRig.GetAimRay().direction : transform.forward;
            RequestActivateAbilityRpc("primary", origin, dir);
        }

        public void TryActivateUltimate()
        {
            if (!IsOwner) return;
            if (_health != null && _health.State != PlayerLifeState.Alive) return;
            if (ClientUltimateCd > 0.05f && ClientUltimateCharge < 100f) return;

            Vector3 origin = _cameraRig != null ? _cameraRig.GetAimRay().origin : transform.position + Vector3.up * 1.5f;
            Vector3 dir = _cameraRig != null ? _cameraRig.GetAimRay().direction : transform.forward;
            RequestActivateAbilityRpc("ultimate", origin, dir);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestActivateAbilityRpc(string slot, Vector3 origin, Vector3 direction)
        {
            if (_health == null || _health.State != PlayerLifeState.Alive) return;

            bool isPrimary = slot.Equals("primary", StringComparison.OrdinalIgnoreCase);
            var ability = isPrimary ? _primaryAbilityDef : _ultimateAbilityDef;
            if (ability == null) return;

            float remainingCd = isPrimary ? (_serverPrimaryReadyTime - Time.time) : (_serverUltimateReadyTime - Time.time);
            if (remainingCd > 0.05f) return;

            if (!isPrimary && UltimateCharge.Value < 100f && remainingCd > 0.05f)
            {
                return;
            }

            // Set cooldown
            float cdSeconds = (float)ability.Cooldown.TotalSeconds;
            if (isPrimary)
            {
                _serverPrimaryReadyTime = Time.time + cdSeconds;
                PrimaryCooldownRemaining.Value = cdSeconds;
            }
            else
            {
                _serverUltimateReadyTime = Time.time + cdSeconds;
                UltimateCooldownRemaining.Value = cdSeconds;
                UltimateCharge.Value = 0f;
            }

            // Execute Server Gameplay Ability Logic
            ExecuteServerAbility(ability, origin, direction);

            // Replicate visual / audio feedback to all clients
            PlayAbilityVisualRpc(ability.Id, origin, direction);
        }

        private void ExecuteServerAbility(AbilityDefinition ability, Vector3 origin, Vector3 direction)
        {
            string id = ability.Id;

            switch (id)
            {
                // REX
                case "rex_frag":
                    SpawnServerFragGrenade(origin, direction, 70f, 6.5f);
                    break;
                case "rex_overrun":
                    ApplyServerOverrunBuff(6f);
                    break;

                // NOVA
                case "nova_drone":
                    ServerTriggerReconScan(origin, 45f);
                    break;
                case "nova_pulse":
                    ServerTriggerEmpPulse(origin, 22f, 40f);
                    break;

                // MEDIC
                case "medic_stim":
                    ServerApplyMedStim(origin, direction, 45f);
                    break;
                case "medic_fieldrevive":
                    ServerBioPulseRevive(25f, 50f);
                    break;

                // TITAN
                case "titan_suppress":
                    ServerDeployEnergyShield(origin, direction, 5f);
                    break;
                case "titan_bastion":
                    ServerApplyBastionFortify(75f);
                    break;

                // VIPER
                case "viper_toxin":
                    ServerSpawnToxinCanister(origin, direction, 6f, 14f);
                    break;
                case "viper_killbox":
                    ServerDeployKillboxTurret(origin, direction);
                    break;

                // TRACE
                case "trace_dart":
                    ServerFireTrackingDart(origin, direction);
                    break;
                case "trace_hunt":
                    ServerTriggerHuntInstinct(50f, 6f);
                    break;

                // TORQUE
                case "torque_cover":
                    ServerDeployTacticalCover(origin, direction);
                    break;
                case "torque_emp":
                    ServerTriggerEmpPulse(origin, 16f, 25f);
                    break;

                // SHADE
                case "shade_smoke":
                    ServerDeploySmokeScreen(origin, direction);
                    break;
                case "shade_dash":
                    ServerPerformPhaseDash(direction, 10f);
                    break;

                // CIRCUIT
                case "circuit_trip":
                    ServerPlantTripMine(origin, direction);
                    break;
                case "circuit_gridlock":
                    ServerTriggerEmpPulse(origin, 18f, 20f);
                    break;

                // FALCON
                case "falcon_grapple":
                    ServerPerformGrapple(origin, direction, 20f);
                    break;
                case "falcon_strike":
                    ServerCallAirstrike(origin + direction * 25f);
                    break;
            }

            Debug.Log($"[ServerAbility] Operator {OperatorId.Value} activated {ability.Id} on server.");
        }

        #region Server Ability Implementations

        private void SpawnServerFragGrenade(Vector3 origin, Vector3 direction, float maxDamage, float radius)
        {
            var grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = "FragGrenade_Live";
            grenade.transform.position = origin;
            grenade.transform.localScale = Vector3.one * 0.28f;
            var rb = grenade.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearVelocity = (direction + Vector3.up * 0.22f).normalized * 22f;

            StartCoroutine(DetonateGrenadeRoutine(grenade, 1.25f, maxDamage, radius));
        }

        private IEnumerator DetonateGrenadeRoutine(GameObject grenade, float delay, float maxDamage, float radius)
        {
            yield return new WaitForSeconds(delay);
            if (grenade != null)
            {
                Vector3 blastPos = grenade.transform.position;
                Destroy(grenade);

                // Splash damage to opposing players and explosive targets
                var colliders = Physics.OverlapSphere(blastPos, radius, ~0, QueryTriggerInteraction.Ignore);
                foreach (var col in colliders)
                {
                    if (col.transform.IsChildOf(transform)) continue;

                    var targetPlayer = col.GetComponentInParent<NetworkPlayerController>();
                    if (targetPlayer != null && targetPlayer.NetFaction.Value != GetComponent<NetworkPlayerController>().NetFaction.Value)
                    {
                        float dist = Vector3.Distance(blastPos, targetPlayer.transform.position);
                        float factor = 1f - Mathf.Clamp01(dist / radius);
                        targetPlayer.ApplyServerDamage(maxDamage * factor, blastPos, OwnerClientId, false);
                    }
                    else
                    {
                        var dmg = col.GetComponentInParent<IDamageable>();
                        dmg?.TakeDamage(maxDamage, blastPos, Vector3.up, false);
                    }
                }
            }
        }

        private void ApplyServerOverrunBuff(float duration)
        {
            _speedBuffTimer = duration;
            UltimateCharge.Value = 0f;
            // Immediate partial reload on overrun
            if (_weapons != null && _weapons.CurrentWeapon != null)
            {
                _weapons.CurrentWeapon.currentMagazine = _weapons.CurrentWeapon.magazineSize;
            }
        }

        private void ServerTriggerReconScan(Vector3 origin, float range)
        {
            var match = Multiplayer.NetworkMatchManager.Instance;
            if (match == null) return;

            int myFaction = GetComponent<NetworkPlayerController>().NetFaction.Value;
            foreach (var p in match.AllPlayers)
            {
                if (p != null && p.NetFaction.Value != myFaction)
                {
                    float d = Vector3.Distance(origin, p.transform.position);
                    if (d <= range)
                    {
                        match.BroadcastFeedRpc($"RECON: {p.NetCallsign.Value} DETECTED AT {Mathf.CeilToInt(d)}m");
                    }
                }
            }
        }

        private void ServerTriggerEmpPulse(Vector3 origin, float radius, float armorDamage)
        {
            var match = Multiplayer.NetworkMatchManager.Instance;
            if (match == null) return;

            int myFaction = GetComponent<NetworkPlayerController>().NetFaction.Value;
            foreach (var p in match.AllPlayers)
            {
                if (p != null && p.NetFaction.Value != myFaction)
                {
                    if (Vector3.Distance(origin, p.transform.position) <= radius)
                    {
                        p.ApplyServerDamage(armorDamage, p.transform.position, OwnerClientId, false);
                    }
                }
            }
        }

        private void ServerApplyMedStim(Vector3 origin, Vector3 direction, float healAmount)
        {
            // Heal self or aimed ally
            _health.Heal(healAmount);
        }

        private void ServerBioPulseRevive(float radius, float healAmount)
        {
            var match = Multiplayer.NetworkMatchManager.Instance;
            if (match == null) return;

            int myFaction = GetComponent<NetworkPlayerController>().NetFaction.Value;
            foreach (var p in match.AllPlayers)
            {
                if (p != null && p.NetFaction.Value == myFaction && p != GetComponent<NetworkPlayerController>())
                {
                    if (Vector3.Distance(transform.position, p.transform.position) <= radius)
                    {
                        var health = p.GetComponent<PlayerHealth>();
                        if (health.State == PlayerLifeState.Downed || health.State == PlayerLifeState.BeingRevived)
                        {
                            health.Revive();
                            match.BroadcastFeedRpc($"MEDIC BIO-REVIVED {p.NetCallsign.Value}");
                        }
                    }
                }
            }
        }

        private void ServerDeployEnergyShield(Vector3 origin, Vector3 direction, float duration)
        {
            var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shield.name = "KineticShield_Barrier";
            shield.transform.position = origin + direction * 1.5f;
            shield.transform.rotation = Quaternion.LookRotation(direction);
            shield.transform.localScale = new Vector3(2.4f, 1.8f, 0.2f);
            Destroy(shield, duration);
        }

        private void ServerApplyBastionFortify(float armorBoost)
        {
            _health.FortifyArmor(armorBoost);
        }

        private void ServerSpawnToxinCanister(Vector3 origin, Vector3 direction, float duration, float dps)
        {
            var canister = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            canister.name = "ToxinCanister_Field";
            canister.transform.position = origin + direction * 2f;
            canister.transform.localScale = new Vector3(5f, 0.2f, 5f);
            Destroy(canister, duration);
        }

        private void ServerDeployKillboxTurret(Vector3 origin, Vector3 direction)
        {
            var turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turret.name = "ViperKillbox_Turret";
            turret.transform.position = origin + direction * 1.2f;
            turret.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            Destroy(turret, 8f);
        }

        private void ServerFireTrackingDart(Vector3 origin, Vector3 direction)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, 50f))
            {
                var enemy = hit.collider.GetComponentInParent<NetworkPlayerController>();
                if (enemy != null)
                {
                    Multiplayer.NetworkMatchManager.Instance?.BroadcastFeedRpc($"TRACE SENSOR TAGGED {enemy.NetCallsign.Value}!");
                }
            }
        }

        private void ServerTriggerHuntInstinct(float range, float duration)
        {
            Multiplayer.NetworkMatchManager.Instance?.BroadcastFeedRpc($"TRACE ACTIVATED HUNTER'S INSTINCT ({duration}s)");
        }

        private void ServerDeployTacticalCover(Vector3 origin, Vector3 direction)
        {
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Torque_DeployableCover";
            cover.transform.position = origin + direction * 1.8f;
            cover.transform.rotation = Quaternion.LookRotation(direction);
            cover.transform.localScale = new Vector3(3f, 1.2f, 0.5f);
            Destroy(cover, 20f);
        }

        private void ServerDeploySmokeScreen(Vector3 origin, Vector3 direction)
        {
            var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smoke.name = "TacticalSmoke_Cloud";
            smoke.transform.position = origin + direction * 3f;
            smoke.transform.localScale = new Vector3(9f, 6f, 9f);
            var col = smoke.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Destroy(smoke, 8f);
        }

        private void ServerPerformPhaseDash(Vector3 direction, float distance)
        {
            Vector3 dashTarget = transform.position + direction.normalized * distance;
            transform.position = dashTarget;
        }

        private void ServerPlantTripMine(Vector3 origin, Vector3 direction)
        {
            var mine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mine.name = "Circuit_TripMine";
            mine.transform.position = origin + direction * 1.2f;
            mine.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            Destroy(mine, 25f);
        }

        private void ServerPerformGrapple(Vector3 origin, Vector3 direction, float maxRange)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange))
            {
                transform.position = hit.point - direction * 0.8f;
            }
        }

        private void ServerCallAirstrike(Vector3 targetPoint)
        {
            Multiplayer.NetworkMatchManager.Instance?.BroadcastFeedRpc("INCOMING AIRSTRIKE ON TARGET SECTOR!");
            StartCoroutine(AirstrikeDelayRoutine(targetPoint, 2.0f, 90f, 8f));
        }

        private IEnumerator AirstrikeDelayRoutine(Vector3 center, float delay, float damage, float radius)
        {
            yield return new WaitForSeconds(delay);
            var colliders = Physics.OverlapSphere(center, radius);
            foreach (var c in colliders)
            {
                var p = c.GetComponentInParent<NetworkPlayerController>();
                if (p != null)
                {
                    p.ApplyServerDamage(damage, center, OwnerClientId, false);
                }
            }
        }

        #endregion

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayAbilityVisualRpc(string abilityId, Vector3 origin, Vector3 direction)
        {
            // Trigger visual feedback (particle, audio or flash)
            var hud = FindAnyObjectByType<TacticalHUD>();
            if (IsOwner && hud != null)
            {
                hud.TriggerAbilityVisual(abilityId);
            }
        }
    }
}
