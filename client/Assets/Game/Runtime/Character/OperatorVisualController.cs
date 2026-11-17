using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Combat;

namespace OutrunAftermath.Client.Runtime.Character
{
    /// <summary>
    /// Manages the visual presentation, modular equipment sockets, first-person/third-person
    /// presentation switching, weapon attachments, and animation parameter synchronization for Operator characters.
    /// </summary>
    [DisallowMultipleComponent]
    public class OperatorVisualController : MonoBehaviour
    {
        [Header("Operator Configuration")]
        [SerializeField] private string operatorId = "rex";
        [SerializeField] private string faction = "Rogue";

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private SkinnedMeshRenderer bodySkinnedMesh;

        [Header("Presentations")]
        [SerializeField] private GameObject thirdPersonRoot;
        [SerializeField] private GameObject firstPersonRoot;
        [SerializeField] private Transform headBone;

        [Header("Equipment Sockets")]
        [SerializeField] private Transform rightHandWeaponSocket;
        [SerializeField] private Transform leftHandSupportSocket;
        [SerializeField] private Transform primaryWeaponHolster;
        [SerializeField] private Transform secondaryWeaponHolster;
        [SerializeField] private Transform backpackSocket;
        [SerializeField] private Transform helmetAttachmentSocket;
        [SerializeField] private Transform shoulderLeftSocket;
        [SerializeField] private Transform shoulderRightSocket;
        [SerializeField] private Transform utilityBeltSocket;

        [Header("Modular Equipment Visuals")]
        [SerializeField] private List<Renderer> thirdPersonEquipmentRenderers = new List<Renderer>();
        [SerializeField] private List<Renderer> headEquipmentRenderers = new List<Renderer>();
        [SerializeField] private List<Renderer> firstPersonRenderers = new List<Renderer>();

        // Associated component references
        private CharacterMotor _motor;
        private WeaponSystem _weapons;
        private PlayerHealth _health;
        private CameraRig _cameraRig;
        private bool _isLocalOwner;
        private CameraPerspective _currentPerspective = CameraPerspective.FirstPerson;
        private int _activeSlotIndex = 0;
        private float _fireAnimationTimer = 0f;

        // Weapon attachment visual instances (slot -> visual GameObject)
        private readonly Dictionary<int, GameObject> _weaponVisualInstances = new Dictionary<int, GameObject>();

        // Animator parameter hashes for high efficiency
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashIsCrouched = Animator.StringToHash("IsCrouched");
        private static readonly int HashIsAiming = Animator.StringToHash("IsAiming");
        private static readonly int HashIsFiring = Animator.StringToHash("IsFiring");
        private static readonly int HashIsReloading = Animator.StringToHash("IsReloading");
        private static readonly int HashHitTrigger = Animator.StringToHash("HitTrigger");
        private static readonly int HashLifeState = Animator.StringToHash("LifeState");
        private static readonly int HashIsReviving = Animator.StringToHash("IsReviving");

        public string OperatorId => operatorId;
        public string Faction => faction;
        public Animator CharacterAnimator => animator;
        public SkinnedMeshRenderer BodyMesh => bodySkinnedMesh;
        public GameObject ThirdPersonRoot => thirdPersonRoot;
        public GameObject FirstPersonRoot => firstPersonRoot;
        public Transform RightHandWeaponSocket => rightHandWeaponSocket;
        public Transform LeftHandSupportSocket => leftHandSupportSocket;
        public Transform PrimaryWeaponHolster => primaryWeaponHolster;
        public Transform SecondaryWeaponHolster => secondaryWeaponHolster;
        public Transform BackpackSocket => backpackSocket;
        public Transform HelmetAttachmentSocket => helmetAttachmentSocket;
        public Transform ShoulderLeftSocket => shoulderLeftSocket;
        public Transform ShoulderRightSocket => shoulderRightSocket;
        public Transform UtilityBeltSocket => utilityBeltSocket;

        public void Initialize(string opId, string opFaction, CharacterMotor motor, WeaponSystem weapons, PlayerHealth health, CameraRig cameraRig, bool isLocalOwner)
        {
            operatorId = opId;
            faction = opFaction;
            _motor = motor;
            _weapons = weapons;
            _health = health;
            _cameraRig = cameraRig;
            _isLocalOwner = isLocalOwner;

            if (_weapons != null)
            {
                _weapons.OnWeaponSwitched += HandleWeaponSwitched;
                _weapons.OnFired += HandleWeaponFired;
            }

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }

            DiscoverSocketsAndRenderers();
            UpdateSocketedWeaponVisuals();
            ApplyPerspectiveMode(_currentPerspective);
        }

        private void Awake()
        {
            DiscoverSocketsAndRenderers();
            UpdateSocketedWeaponVisuals();
        }

        private void OnDestroy()
        {
            if (_weapons != null)
            {
                _weapons.OnWeaponSwitched -= HandleWeaponSwitched;
                _weapons.OnFired -= HandleWeaponFired;
            }

            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
            }
        }

        public void DiscoverSocketsAndRenderers()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (bodySkinnedMesh == null) bodySkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (thirdPersonRoot == null)
            {
                var tp = transform.Find("ThirdPersonPresentation");
                if (tp != null) thirdPersonRoot = tp.gameObject;
            }

            if (firstPersonRoot == null)
            {
                var fp = transform.Find("FirstPersonPresentation");
                if (fp != null) firstPersonRoot = fp.gameObject;
            }

            // Find Sockets by standard name
            rightHandWeaponSocket = FindSocketByName("RightHandWeaponSocket", rightHandWeaponSocket);
            leftHandSupportSocket = FindSocketByName("LeftHandSupportSocket", leftHandSupportSocket);
            primaryWeaponHolster = FindSocketByName("PrimaryWeaponHolster", primaryWeaponHolster);
            secondaryWeaponHolster = FindSocketByName("SecondaryWeaponHolster", secondaryWeaponHolster);
            backpackSocket = FindSocketByName("BackpackSocket", backpackSocket);
            helmetAttachmentSocket = FindSocketByName("HelmetAttachmentSocket", helmetAttachmentSocket);
            shoulderLeftSocket = FindSocketByName("ShoulderLeftSocket", shoulderLeftSocket);
            shoulderRightSocket = FindSocketByName("ShoulderRightSocket", shoulderRightSocket);
            utilityBeltSocket = FindSocketByName("UtilityBeltSocket", utilityBeltSocket);

            if (headBone == null)
            {
                headBone = transform.Find("ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/Neck/Head");
            }

            // Collect renderers
            thirdPersonEquipmentRenderers.Clear();
            headEquipmentRenderers.Clear();
            firstPersonRenderers.Clear();

            if (thirdPersonRoot != null)
            {
                foreach (var r in thirdPersonRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == bodySkinnedMesh) continue;
                    if (r.name.Contains("Helmet") || r.name.Contains("Headset"))
                    {
                        headEquipmentRenderers.Add(r);
                    }
                    else
                    {
                        thirdPersonEquipmentRenderers.Add(r);
                    }
                }
            }

            if (firstPersonRoot != null)
            {
                firstPersonRenderers.AddRange(firstPersonRoot.GetComponentsInChildren<Renderer>(true));
            }
        }

        private Transform FindSocketByName(string socketName, Transform fallback)
        {
            if (fallback != null) return fallback;
            var found = transform.GetComponentsInChildren<Transform>(true);
            foreach (var t in found)
            {
                if (t.name.Equals(socketName, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

        private void Update()
        {
            if (_fireAnimationTimer > 0f)
            {
                _fireAnimationTimer -= Time.deltaTime;
            }

            if (_isLocalOwner)
            {
                UpdateAnimationParameters();
            }
        }

        private void UpdateAnimationParameters()
        {
            if (animator == null || !animator.gameObject.activeInHierarchy) return;

            float speed = _motor != null ? _motor.Velocity.magnitude : 0f;
            bool grounded = _motor != null && _motor.IsGrounded;
            bool crouched = _motor != null && (_motor.Stance == MovementStance.Crouching || _motor.Stance == MovementStance.Sliding);
            bool aiming = _cameraRig != null && _cameraRig.IsAiming;
            bool firing = _fireAnimationTimer > 0f;
            bool reloading = _weapons != null && _weapons.IsReloading;
            int lifeState = _health != null ? (int)_health.State : 0;
            bool reviving = _health != null && _health.State == PlayerLifeState.BeingRevived;

            animator.SetFloat(HashSpeed, speed);
            animator.SetBool(HashIsGrounded, grounded);
            animator.SetBool(HashIsCrouched, crouched);
            animator.SetBool(HashIsAiming, aiming);
            animator.SetBool(HashIsFiring, firing);
            animator.SetBool(HashIsReloading, reloading);
            animator.SetInteger(HashLifeState, lifeState);
            animator.SetBool(HashIsReviving, reviving);
        }

        /// <summary>
        /// Replicates animation parameters from network variables for remote player proxies.
        /// </summary>
        public void UpdateRemoteAnimationParameters(float speed, bool isGrounded, bool isCrouched, bool isAiming, bool isReloading, int activeSlot, int lifeState, bool isReviving)
        {
            if (_isLocalOwner) return; // Local owner uses local motor/camera/weapons
            if (animator == null || !animator.gameObject.activeInHierarchy) return;

            animator.SetFloat(HashSpeed, speed);
            animator.SetBool(HashIsGrounded, isGrounded);
            animator.SetBool(HashIsCrouched, isCrouched);
            animator.SetBool(HashIsAiming, isAiming);
            animator.SetBool(HashIsFiring, _fireAnimationTimer > 0f);
            animator.SetBool(HashIsReloading, isReloading);
            animator.SetInteger(HashLifeState, lifeState);
            animator.SetBool(HashIsReviving, isReviving);

            if (_activeSlotIndex != activeSlot)
            {
                SetActiveSlotIndex(activeSlot);
            }
        }

        public void SetActiveSlotIndex(int slotIndex)
        {
            _activeSlotIndex = slotIndex;
            UpdateSocketedWeaponVisuals();
        }

        private void HandleWeaponSwitched(WeaponData weapon)
        {
            if (_weapons != null)
            {
                _activeSlotIndex = _weapons.ActiveSlotIndex;
            }
            UpdateSocketedWeaponVisuals();
        }

        public void HandleWeaponFired()
        {
            _fireAnimationTimer = 0.15f;
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetBool(HashIsFiring, true);
            }
        }

        public void TriggerHitReaction()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(HashHitTrigger);
            }
        }

        private void HandleDamaged(float dmg, Vector3 pt)
        {
            TriggerHitReaction();
        }

        public void SetPerspective(CameraPerspective perspective)
        {
            _currentPerspective = perspective;
            ApplyPerspectiveMode(perspective);
        }

        public void ApplyPerspectiveMode(CameraPerspective perspective)
        {
            if (!_isLocalOwner)
            {
                // Remote clients always render in full Third-Person with full shadows
                if (firstPersonRoot != null) firstPersonRoot.SetActive(false);
                if (thirdPersonRoot != null) thirdPersonRoot.SetActive(true);
                if (bodySkinnedMesh != null)
                {
                    bodySkinnedMesh.shadowCastingMode = ShadowCastingMode.On;
                    bodySkinnedMesh.enabled = true;
                }
                foreach (var r in headEquipmentRenderers) if (r != null) { r.shadowCastingMode = ShadowCastingMode.On; r.enabled = true; }
                foreach (var r in thirdPersonEquipmentRenderers) if (r != null) { r.shadowCastingMode = ShadowCastingMode.On; r.enabled = true; }
                SetSocketedWeaponShadowMode(ShadowCastingMode.On);
                return;
            }

            if (perspective == CameraPerspective.FirstPerson)
            {
                // First Person Presentation:
                // Dedicated first-person arms viewmodel active
                if (firstPersonRoot != null) firstPersonRoot.SetActive(true);

                // Third-person body mesh casts shadows but does not clip the local camera
                if (bodySkinnedMesh != null)
                {
                    bodySkinnedMesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    bodySkinnedMesh.enabled = true;
                }

                // Hide head and helmet from local camera to prevent clipping inside head mesh
                foreach (var r in headEquipmentRenderers)
                {
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                foreach (var r in thirdPersonEquipmentRenderers)
                {
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                SetSocketedWeaponShadowMode(ShadowCastingMode.ShadowsOnly);
            }
            else
            {
                // Third Person Presentation:
                // Disable first person arms
                if (firstPersonRoot != null) firstPersonRoot.SetActive(false);

                // Third person body and gear are fully visible with shadows
                if (bodySkinnedMesh != null)
                {
                    bodySkinnedMesh.shadowCastingMode = ShadowCastingMode.On;
                    bodySkinnedMesh.enabled = true;
                }

                foreach (var r in headEquipmentRenderers)
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.enabled = true;
                    }
                }

                foreach (var r in thirdPersonEquipmentRenderers)
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.enabled = true;
                    }
                }

                SetSocketedWeaponShadowMode(ShadowCastingMode.On);
            }
        }

        private void SetSocketedWeaponShadowMode(ShadowCastingMode mode)
        {
            foreach (var kvp in _weaponVisualInstances)
            {
                if (kvp.Value == null) continue;
                foreach (var r in kvp.Value.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = mode;
                }
            }
        }

        // ==============================================================================
        // C.1.4 WEAPON ATTACHMENT SYSTEM IMPLEMENTATION
        // ==============================================================================

        /// <summary>
        /// Updates the attachment of active and holstered weapon models across the 9 standardized sockets.
        /// Ensures active weapon is in RightHand, inactive primary is slung on back, and sidearm is holstered on thigh.
        /// Guarantees zero duplicate allocations on weapon switching.
        /// </summary>
        public void UpdateSocketedWeaponVisuals()
        {
            if (rightHandWeaponSocket == null)
            {
                DiscoverSocketsAndRenderers();
            }

            // Ensure weapon instances for loadout slots 0 (AR), 1 (Shotgun), 2 (Pistol)
            EnsureSocketedWeaponInstance(0, "PF_Weapon_Vanguard_AssaultRifle");
            EnsureSocketedWeaponInstance(1, "PF_Weapon_Breacher_Shotgun");
            EnsureSocketedWeaponInstance(2, "PF_Weapon_Sidekick_Pistol");

            // Attach active weapon to RightHandWeaponSocket
            if (_weaponVisualInstances.TryGetValue(_activeSlotIndex, out var activeVisual) && activeVisual != null)
            {
                if (rightHandWeaponSocket != null)
                {
                    activeVisual.transform.SetParent(rightHandWeaponSocket, false);
                    activeVisual.transform.localPosition = GetWeaponHandOffset(_activeSlotIndex);
                    activeVisual.transform.localRotation = GetWeaponHandRotation(_activeSlotIndex);
                    activeVisual.transform.localScale = Vector3.one;
                    activeVisual.SetActive(true);
                }
            }

            // Attach inactive primary rifle/shotgun to PrimaryWeaponHolster (on back)
            int inactivePrimarySlot = (_activeSlotIndex == 0) ? 1 : 0;
            if (_weaponVisualInstances.TryGetValue(inactivePrimarySlot, out var holsteredPrimary) && holsteredPrimary != null)
            {
                if (primaryWeaponHolster != null)
                {
                    holsteredPrimary.transform.SetParent(primaryWeaponHolster, false);
                    holsteredPrimary.transform.localPosition = Vector3.zero;
                    holsteredPrimary.transform.localRotation = Quaternion.identity;
                    holsteredPrimary.transform.localScale = Vector3.one;
                    holsteredPrimary.SetActive(true);
                }
            }

            // Attach sidearm pistol to SecondaryWeaponHolster (on thigh)
            if (_activeSlotIndex != 2)
            {
                if (_weaponVisualInstances.TryGetValue(2, out var holsteredSidearm) && holsteredSidearm != null)
                {
                    if (secondaryWeaponHolster != null)
                    {
                        holsteredSidearm.transform.SetParent(secondaryWeaponHolster, false);
                        holsteredSidearm.transform.localPosition = new Vector3(0.02f, 0.04f, 0f);
                        holsteredSidearm.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                        holsteredSidearm.transform.localScale = Vector3.one;
                        holsteredSidearm.SetActive(true);
                    }
                }
            }

            // Refresh shadow casting mode based on current perspective
            ShadowCastingMode currentMode = (_isLocalOwner && _currentPerspective == CameraPerspective.FirstPerson)
                ? ShadowCastingMode.ShadowsOnly
                : ShadowCastingMode.On;
            SetSocketedWeaponShadowMode(currentMode);
        }

        private Vector3 GetWeaponHandOffset(int slotIndex)
        {
            return slotIndex switch
            {
                1 => new Vector3(0.02f, -0.04f, 0.12f), // Breacher-12 Shotgun
                2 => new Vector3(0.01f, -0.03f, 0.08f), // Sidekick-9 Pistol
                _ => new Vector3(0.02f, -0.04f, 0.14f)  // Vanguard-4 AR
            };
        }

        private Quaternion GetWeaponHandRotation(int slotIndex)
        {
            return slotIndex switch
            {
                2 => Quaternion.Euler(0f, 90f, 0f),
                _ => Quaternion.Euler(0f, 90f, 0f)
            };
        }

        private void EnsureSocketedWeaponInstance(int slotIndex, string prefabName)
        {
            if (_weaponVisualInstances.TryGetValue(slotIndex, out var go) && go != null)
            {
                return;
            }

            // 1. Try loading production weapon prefab from Resources
            var prefab = Resources.Load<GameObject>($"Art/Prefabs/{prefabName}");
            if (prefab != null)
            {
                var inst = Instantiate(prefab, transform);
                inst.name = $"{prefabName}_Socketed_{slotIndex}";
                _weaponVisualInstances[slotIndex] = inst;
                return;
            }

            // 2. Try loading Wavefront OBJ mesh as fallback
            string objName = slotIndex switch
            {
                1 => "Weapon_Breacher_Shotgun",
                2 => "Weapon_Sidekick_Pistol",
                _ => "Weapon_Vanguard_AssaultRifle"
            };

            var mesh = Resources.Load<Mesh>($"Art/Meshes/Props/{objName}");
            if (mesh != null)
            {
                var inst = new GameObject($"{objName}_Socketed_{slotIndex}");
                inst.transform.SetParent(transform, false);
                var mf = inst.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = inst.AddComponent<MeshRenderer>();
                var mat = Resources.Load<Material>("Art/Materials/M_Weapon_TacticalSteel");
                if (mat != null) mr.sharedMaterial = mat;
                _weaponVisualInstances[slotIndex] = inst;
            }
        }
    }
}
