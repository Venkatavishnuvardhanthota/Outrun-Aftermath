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

        [Header("Support Hand IK & Foregrip")]
        [SerializeField] private bool enableSupportHandIk = true;
        private Transform _activeForegripTarget;
        private bool _isTwoHandedGrip = false;

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

        public Transform ActiveForegripTarget => _activeForegripTarget;
        public bool IsTwoHandedGrip => _isTwoHandedGrip;
        public int ActiveSlotIndex => _activeSlotIndex;
        public IReadOnlyDictionary<int, GameObject> WeaponVisualInstances => _weaponVisualInstances;

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

            if (leftHandSupportSocket == null)
            {
                leftHandSupportSocket = transform.Find("ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand");
            }

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
        /// Guarantees that EVERY supported loadout weapon has exactly one valid parent:
        /// - Active weapon -> RightHandWeaponSocket
        /// - Inactive primary rifle / shotgun -> PrimaryWeaponHolster
        /// - Inactive sidearm pistol -> SecondaryWeaponHolster
        /// Guarantees zero duplicate allocations on weapon switching.
        /// </summary>
        public void UpdateSocketedWeaponVisuals()
        {
            if (rightHandWeaponSocket == null || primaryWeaponHolster == null || secondaryWeaponHolster == null)
            {
                DiscoverSocketsAndRenderers();
            }

            if (rightHandWeaponSocket == null)
            {
                Debug.LogError("[OperatorVisualController] Missing required RightHandWeaponSocket!");
            }
            if (primaryWeaponHolster == null)
            {
                Debug.LogError("[OperatorVisualController] Missing required PrimaryWeaponHolster!");
            }
            if (secondaryWeaponHolster == null)
            {
                Debug.LogError("[OperatorVisualController] Missing required SecondaryWeaponHolster!");
            }

            // Ensure weapon instances for loadout slots 0 (AR), 1 (Shotgun), 2 (Pistol)
            EnsureSocketedWeaponInstance(0, "PF_Weapon_Vanguard_AssaultRifle");
            EnsureSocketedWeaponInstance(1, "PF_Weapon_Breacher_Shotgun");
            EnsureSocketedWeaponInstance(2, "PF_Weapon_Sidekick_Pistol");

            // Pre-create foregrip sockets for all two-handed weapons in loadout so transform hierarchy remains strictly invariant
            if (_weaponVisualInstances.TryGetValue(0, out var rGo) && rGo != null)
                GetOrCreateForegripSocket(rGo, new Vector3(-0.03f, -0.02f, 0.02f));
            if (_weaponVisualInstances.TryGetValue(1, out var sGo) && sGo != null)
                GetOrCreateForegripSocket(sGo, new Vector3(-0.03f, -0.02f, 0.01f));

            // 1. Process Slot 0 (Vanguard-4 Assault Rifle)
            if (_weaponVisualInstances.TryGetValue(0, out var rifleVisual) && rifleVisual != null)
            {
                if (_activeSlotIndex == 0)
                {
                    if (rightHandWeaponSocket != null)
                    {
                        rifleVisual.transform.SetParent(rightHandWeaponSocket, false);
                        rifleVisual.transform.localPosition = GetWeaponHandOffset(0);
                        rifleVisual.transform.localRotation = GetWeaponHandRotation(0);
                        rifleVisual.transform.localScale = Vector3.one;
                        rifleVisual.SetActive(true);
                    }
                }
                else
                {
                    if (primaryWeaponHolster != null)
                    {
                        rifleVisual.transform.SetParent(primaryWeaponHolster, false);
                        rifleVisual.transform.localPosition = Vector3.zero;
                        rifleVisual.transform.localRotation = Quaternion.identity;
                        rifleVisual.transform.localScale = Vector3.one;
                        rifleVisual.SetActive(true);
                    }
                }
            }

            // 2. Process Slot 1 (Breacher-12 Shotgun)
            if (_weaponVisualInstances.TryGetValue(1, out var shotgunVisual) && shotgunVisual != null)
            {
                if (_activeSlotIndex == 1)
                {
                    if (rightHandWeaponSocket != null)
                    {
                        shotgunVisual.transform.SetParent(rightHandWeaponSocket, false);
                        shotgunVisual.transform.localPosition = GetWeaponHandOffset(1);
                        shotgunVisual.transform.localRotation = GetWeaponHandRotation(1);
                        shotgunVisual.transform.localScale = Vector3.one;
                        shotgunVisual.SetActive(true);
                    }
                }
                else
                {
                    if (primaryWeaponHolster != null)
                    {
                        shotgunVisual.transform.SetParent(primaryWeaponHolster, false);
                        // Offset slightly so it nests cleanly next to slung rifle when both are holstered
                        shotgunVisual.transform.localPosition = new Vector3(-0.06f, 0.05f, -0.03f);
                        shotgunVisual.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                        shotgunVisual.transform.localScale = Vector3.one;
                        shotgunVisual.SetActive(true);
                    }
                }
            }

            // 3. Process Slot 2 (Sidekick-9 Pistol)
            if (_weaponVisualInstances.TryGetValue(2, out var pistolVisual) && pistolVisual != null)
            {
                if (_activeSlotIndex == 2)
                {
                    if (rightHandWeaponSocket != null)
                    {
                        pistolVisual.transform.SetParent(rightHandWeaponSocket, false);
                        pistolVisual.transform.localPosition = GetWeaponHandOffset(2);
                        pistolVisual.transform.localRotation = GetWeaponHandRotation(2);
                        pistolVisual.transform.localScale = Vector3.one;
                        pistolVisual.SetActive(true);
                    }
                }
                else
                {
                    if (secondaryWeaponHolster != null)
                    {
                        pistolVisual.transform.SetParent(secondaryWeaponHolster, false);
                        pistolVisual.transform.localPosition = new Vector3(0.02f, 0.04f, 0f);
                        pistolVisual.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                        pistolVisual.transform.localScale = Vector3.one;
                        pistolVisual.SetActive(true);
                    }
                }
            }

            // 4. Update Support Hand Foregrip Target and align support hand
            UpdateSupportHandTarget();
            AlignSupportHandToForegrip();

            // Refresh shadow casting mode based on current perspective
            ShadowCastingMode currentMode = (_isLocalOwner && _currentPerspective == CameraPerspective.FirstPerson)
                ? ShadowCastingMode.ShadowsOnly
                : ShadowCastingMode.On;
            SetSocketedWeaponShadowMode(currentMode);
        }

        private void UpdateSupportHandTarget()
        {
            if (_activeSlotIndex == 0) // Vanguard-4 AR (Two-handed)
            {
                if (_weaponVisualInstances.TryGetValue(0, out var rifle) && rifle != null)
                {
                    _activeForegripTarget = GetOrCreateForegripSocket(rifle, new Vector3(-0.03f, -0.02f, 0.02f));
                    _isTwoHandedGrip = true;
                }
            }
            else if (_activeSlotIndex == 1) // Breacher-12 Shotgun (Two-handed)
            {
                if (_weaponVisualInstances.TryGetValue(1, out var shotgun) && shotgun != null)
                {
                    _activeForegripTarget = GetOrCreateForegripSocket(shotgun, new Vector3(-0.03f, -0.02f, 0.01f));
                    _isTwoHandedGrip = true;
                }
            }
            else // Sidekick-9 Pistol (Single-handed / tactical sidearm)
            {
                _activeForegripTarget = null;
                _isTwoHandedGrip = false;
            }
        }

        private Transform GetOrCreateForegripSocket(GameObject weaponGo, Vector3 localOffset)
        {
            var existing = weaponGo.transform.Find("Socket_Foregrip");
            if (existing != null) return existing;

            var foregrip = new GameObject("Socket_Foregrip");
            foregrip.transform.SetParent(weaponGo.transform, false);
            foregrip.transform.localPosition = localOffset;
            foregrip.transform.localRotation = Quaternion.identity;
            return foregrip.transform;
        }

        // ==============================================================================
        // TWO-BONE INVERSE KINEMATICS & FOREGRIP SUPPORT-HAND ALIGNMENT
        // ==============================================================================

        [Serializable]
        public struct TwoBoneIKResult
        {
            public bool success;
            public float initialUpperArmLength;
            public float finalUpperArmLength;
            public float initialForearmLength;
            public float finalForearmLength;
            public float targetDistance;
            public float achievedHandDistance;
            public float residualError;
            public float elbowAngle;
            public bool isReachable;
        }

        public TwoBoneIKResult LastIKResult { get; private set; }

        private void OnAnimatorIK(int layerIndex)
        {
            // Authoritative IK ownership architecture:
            // Mecanim's built-in SetIKPosition/SetIKRotation on AvatarIKGoal.LeftHand is intentionally bypassed.
            // OperatorVisualController's custom analytic Two-Bone IK solver in LateUpdate() is the sole authoritative
            // system responsible for left-hand foregrip placement across both first-person shadow casting and third-person
            // rendering. Bypassing Mecanim IK prevents competing solvers from fighting over the same joint chain in the
            // same frame, guarantees strict bone segment length preservation, and ensures deterministic alignment after
            // all locomotion and combat blend layers evaluate.
        }

        private void LateUpdate()
        {
            // Authoritative IK execution: runs post-Mecanim in LateUpdate to adapt the animated arm pose
            // to the active weapon's physical foregrip socket without bone stretching or joint jitter.
            if (enableSupportHandIk && _isTwoHandedGrip && _activeForegripTarget != null)
            {
                AlignSupportHandToForegrip();
            }
        }

        public void AlignSupportHandToForegrip()
        {
            if (!_isTwoHandedGrip || _activeForegripTarget == null) return;

            Transform leftUpperArm = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            Transform leftLowerArm = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            Transform leftHandBone = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;

            if (leftUpperArm == null || leftLowerArm == null || leftHandBone == null)
            {
                Transform skeletonRoot = transform.Find("ThirdPersonPresentation/Skeleton");
                if (skeletonRoot != null)
                {
                    leftUpperArm = leftUpperArm ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm");
                    leftLowerArm = leftLowerArm ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm");
                    leftHandBone = leftHandBone ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand");
                }
            }

            Quaternion targetHandRot = _activeForegripTarget.rotation * Quaternion.Euler(0f, 90f, 0f);

            if (leftUpperArm != null && leftLowerArm != null && leftHandBone != null)
            {
                // Elbow pole hint: natural downward and outward bias for tactical rifle grip
                Vector3 elbowHint = leftUpperArm.position - transform.up * 0.5f - transform.right * 0.35f;
                LastIKResult = SolveTwoBoneIKDetailed(leftUpperArm, leftLowerArm, leftHandBone, _activeForegripTarget.position, targetHandRot, elbowHint, 1.0f);
            }
            else if (leftHandBone != null)
            {
                leftHandBone.position = _activeForegripTarget.position;
                leftHandBone.rotation = targetHandRot;
            }

            if (leftHandSupportSocket != null)
            {
                leftHandSupportSocket.position = _activeForegripTarget.position;
                leftHandSupportSocket.rotation = targetHandRot;
            }
        }

        /// <summary>
        /// Analytic Two-Bone IK solver using Law of Cosines for human arm/leg chains.
        /// Preserves bone segment lengths strictly through pure joint rotations.
        /// Does NOT distort the skeleton by directly translating the end joint.
        /// </summary>
        public static bool SolveTwoBoneIK(Transform rootJoint, Transform midJoint, Transform endJoint, Vector3 targetPos, Quaternion targetRot, Vector3 hintPos, float weight = 1.0f)
        {
            var res = SolveTwoBoneIKDetailed(rootJoint, midJoint, endJoint, targetPos, targetRot, hintPos, weight);
            return res.success;
        }

        /// <summary>
        /// Comprehensive analytic Two-Bone IK solver returning full numerical metrics for verification.
        /// Strict length preservation: finalUpperArmLength == initialUpperArmLength and finalForearmLength == initialForearmLength.
        /// </summary>
        public static TwoBoneIKResult SolveTwoBoneIKDetailed(Transform rootJoint, Transform midJoint, Transform endJoint, Vector3 targetPos, Quaternion targetRot, Vector3 hintPos, float weight = 1.0f)
        {
            var result = new TwoBoneIKResult();
            if (rootJoint == null || midJoint == null || endJoint == null)
            {
                result.success = false;
                return result;
            }

            Vector3 a = rootJoint.position;
            Vector3 b = midJoint.position;
            Vector3 c = endJoint.position;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            result.initialUpperArmLength = l1;
            result.initialForearmLength = l2;

            if (l1 < 0.001f || l2 < 0.001f)
            {
                result.success = false;
                return result;
            }

            float d = Vector3.Distance(a, targetPos);
            result.targetDistance = d;

            // Strict reachability boundaries (with numerical stability epsilon)
            float maxReach = (l1 + l2) * 0.9999f;
            float minReach = Mathf.Max(0.001f, Mathf.Abs(l1 - l2) * 1.0001f);
            result.isReachable = (d >= minReach && d <= maxReach);

            float clampedDist = Mathf.Clamp(d, minReach, maxReach);
            result.residualError = Mathf.Abs(d - clampedDist);

            if (weight <= 0f)
            {
                result.success = true;
                result.finalUpperArmLength = l1;
                result.finalForearmLength = l2;
                result.achievedHandDistance = Vector3.Distance(endJoint.position, targetPos);
                Vector3 u0 = (a - b).normalized;
                Vector3 v0 = (c - b).normalized;
                result.elbowAngle = Vector3.Angle(u0, v0);
                return result;
            }

            // Direction to target (with safe fallback for near-zero distance target)
            Vector3 toTarget = (targetPos - a);
            if (d < 0.0001f)
            {
                toTarget = rootJoint.forward;
            }
            else
            {
                toTarget = toTarget.normalized;
            }

            Vector3 clampedTargetPos = a + toTarget * clampedDist;

            // Law of Cosines for shoulder angle alpha
            float cosAlpha = Mathf.Clamp((l1 * l1 + clampedDist * clampedDist - l2 * l2) / (2f * l1 * clampedDist), -1f, 1f);
            float angleAlpha = Mathf.Acos(cosAlpha) * Mathf.Rad2Deg;

            // Law of Cosines for elbow interior angle beta
            float cosBeta = Mathf.Clamp((l1 * l1 + l2 * l2 - clampedDist * clampedDist) / (2f * l1 * l2), -1f, 1f);
            float angleBeta = Mathf.Acos(cosBeta) * Mathf.Rad2Deg;
            result.elbowAngle = angleBeta;

            // Determine bend plane using pole vector hint
            Vector3 toHint = (hintPos - a);
            if (toHint.sqrMagnitude < 0.0001f)
            {
                toHint = -rootJoint.up - rootJoint.right * 0.5f;
            }
            else
            {
                toHint = toHint.normalized;
            }

            Vector3 planeNormal = Vector3.Cross(toTarget, toHint);
            if (planeNormal.sqrMagnitude < 0.0001f)
            {
                planeNormal = Vector3.Cross(toTarget, rootJoint.up);
                if (planeNormal.sqrMagnitude < 0.0001f)
                    planeNormal = Vector3.Cross(toTarget, rootJoint.right);
            }
            planeNormal.Normalize();

            Vector3 bendDir = Vector3.Cross(planeNormal, toTarget).normalized;
            if (Vector3.Dot(bendDir, toHint) < 0f)
            {
                bendDir = -bendDir;
            }

            // Calculate elbow position in bend plane
            Vector3 candidate1 = (Quaternion.AngleAxis(angleAlpha, planeNormal) * toTarget).normalized;
            Vector3 candidate2 = (Quaternion.AngleAxis(-angleAlpha, planeNormal) * toTarget).normalized;
            Vector3 upperArmDir = Vector3.Dot(candidate1, bendDir) > Vector3.Dot(candidate2, bendDir) ? candidate1 : candidate2;

            Vector3 desiredElbowPos = a + upperArmDir * l1;

            // Record original rotations for smooth weighting
            Quaternion origRootRot = rootJoint.rotation;
            Quaternion origMidRot = midJoint.rotation;
            Quaternion origEndRot = endJoint.rotation;

            // 1. Rotate rootJoint so midJoint points towards desiredElbowPos
            Vector3 currentRootToMid = (midJoint.position - a);
            Vector3 desiredRootToMid = (desiredElbowPos - a);
            if (currentRootToMid.sqrMagnitude > 0.00001f && desiredRootToMid.sqrMagnitude > 0.00001f)
            {
                Quaternion rootDelta = Quaternion.FromToRotation(currentRootToMid, desiredRootToMid);
                rootJoint.rotation = rootDelta * rootJoint.rotation;
            }

            // 2. Rotate midJoint so endJoint points towards clampedTargetPos
            Vector3 currentMidToEnd = (endJoint.position - midJoint.position);
            Vector3 desiredMidToEnd = (clampedTargetPos - midJoint.position);
            if (currentMidToEnd.sqrMagnitude > 0.00001f && desiredMidToEnd.sqrMagnitude > 0.00001f)
            {
                Quaternion midDelta = Quaternion.FromToRotation(currentMidToEnd, desiredMidToEnd);
                midJoint.rotation = midDelta * midJoint.rotation;
            }

            // Store full solved rotations
            Quaternion solvedRootRot = rootJoint.rotation;
            Quaternion solvedMidRot = midJoint.rotation;

            // Blend with weight if weight < 1.0
            if (weight < 1.0f)
            {
                rootJoint.rotation = Quaternion.Slerp(origRootRot, solvedRootRot, weight);
                midJoint.rotation = Quaternion.Slerp(origMidRot, solvedMidRot, weight);
            }

            // 3. Rotate endJoint (wrist orientation) without moving its position
            endJoint.rotation = Quaternion.Slerp(origEndRot, targetRot, weight);

            // CRITICAL VERIFICATION: DO NOT set endJoint.position directly!
            // Rigid joint rotations strictly preserve upper arm length and forearm length.
            result.finalUpperArmLength = Vector3.Distance(rootJoint.position, midJoint.position);
            result.finalForearmLength = Vector3.Distance(midJoint.position, endJoint.position);
            result.achievedHandDistance = Vector3.Distance(endJoint.position, targetPos);
            result.success = true;

            return result;
        }

        public float GetSupportHandDistance()
        {
            if (!_isTwoHandedGrip || _activeForegripTarget == null) return 0f;

            Transform leftHandBone = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (leftHandBone == null)
            {
                leftHandBone = transform.Find("ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand");
            }

            if (leftHandBone != null)
                return Vector3.Distance(leftHandBone.position, _activeForegripTarget.position);

            if (leftHandSupportSocket != null)
                return Vector3.Distance(leftHandSupportSocket.position, _activeForegripTarget.position);

            return float.MaxValue;
        }

        public float GetLeftElbowAngle()
        {
            Transform leftUpperArm = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            Transform leftLowerArm = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            Transform leftHand = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;

            if (leftUpperArm == null || leftLowerArm == null || leftHand == null)
            {
                Transform skeletonRoot = transform.Find("ThirdPersonPresentation/Skeleton");
                if (skeletonRoot != null)
                {
                    leftUpperArm = leftUpperArm ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm");
                    leftLowerArm = leftLowerArm ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm");
                    leftHand = leftHand ?? skeletonRoot.Find("Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand");
                }
            }

            if (leftUpperArm == null || leftLowerArm == null || leftHand == null) return 0f;

            Vector3 upperToElbow = (leftLowerArm.position - leftUpperArm.position).normalized;
            Vector3 elbowToHand = (leftHand.position - leftLowerArm.position).normalized;
            return Vector3.Angle(-upperToElbow, elbowToHand);
        }

        public bool IsSupportHandAligned(float tolerance = 0.08f)
        {
            if (!_isTwoHandedGrip || _activeForegripTarget == null) return true; // Single handed weapon has no foregrip constraint

            Transform checkTransform = (animator != null) ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (checkTransform == null)
            {
                checkTransform = transform.Find("ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand");
            }
            if (checkTransform == null)
            {
                checkTransform = leftHandSupportSocket;
            }

            if (checkTransform == null) return false;
            return Vector3.Distance(checkTransform.position, _activeForegripTarget.position) <= tolerance;
        }

        public bool AreAllWeaponsCorrectlyParented(int expectedActiveSlot)
        {
            if (rightHandWeaponSocket == null || primaryWeaponHolster == null || secondaryWeaponHolster == null)
                return false;

            int inHandCount = 0;
            foreach (var kvp in _weaponVisualInstances)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value.transform.parent == rightHandWeaponSocket) inHandCount++;
            }
            if (inHandCount != 1) return false;

            if (_weaponVisualInstances.TryGetValue(expectedActiveSlot, out var activeWp) && activeWp != null)
            {
                if (activeWp.transform.parent != rightHandWeaponSocket) return false;
            }
            else return false;

            if (expectedActiveSlot != 0 && _weaponVisualInstances.TryGetValue(0, out var w0) && w0 != null)
            {
                if (w0.transform.parent != primaryWeaponHolster) return false;
            }
            if (expectedActiveSlot != 1 && _weaponVisualInstances.TryGetValue(1, out var w1) && w1 != null)
            {
                if (w1.transform.parent != primaryWeaponHolster) return false;
            }
            if (expectedActiveSlot != 2 && _weaponVisualInstances.TryGetValue(2, out var w2) && w2 != null)
            {
                if (w2.transform.parent != secondaryWeaponHolster) return false;
            }

            return true;
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
