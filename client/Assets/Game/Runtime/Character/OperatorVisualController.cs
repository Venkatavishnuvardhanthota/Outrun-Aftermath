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
    /// presentation switching, and animation parameter synchronization for Operator characters.
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
            ApplyPerspectiveMode(_currentPerspective);
        }

        private void Awake()
        {
            DiscoverSocketsAndRenderers();
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
                headBone = transform.Find("Skeleton/Root/Hips/Spine/Chest/UpperChest/Neck/Head");
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
            UpdateAnimationParameters();
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

        private void HandleWeaponSwitched(WeaponData weapon)
        {
            if (_weapons != null)
            {
                _activeSlotIndex = _weapons.ActiveSlotIndex;
            }
            UpdateSocketedWeaponVisuals();
        }

        private void HandleWeaponFired()
        {
            _fireAnimationTimer = 0.15f;
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetBool(HashIsFiring, true);
            }
        }

        private void HandleDamaged(float dmg, Vector3 pt)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(HashHitTrigger);
            }
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
                // Remote clients always render in full Third-Person
                if (firstPersonRoot != null) firstPersonRoot.SetActive(false);
                if (thirdPersonRoot != null) thirdPersonRoot.SetActive(true);
                if (bodySkinnedMesh != null)
                {
                    bodySkinnedMesh.shadowCastingMode = ShadowCastingMode.On;
                    bodySkinnedMesh.enabled = true;
                }
                foreach (var r in headEquipmentRenderers) if (r != null) r.enabled = true;
                foreach (var r in thirdPersonEquipmentRenderers) if (r != null) r.enabled = true;
                return;
            }

            if (perspective == CameraPerspective.FirstPerson)
            {
                // First Person Presentation:
                // Dedicated first-person arms viewmodel active
                if (firstPersonRoot != null) firstPersonRoot.SetActive(true);

                // Third-person body mesh casts shadows but doesn't occlude the local camera view
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
            }
        }

        public void UpdateSocketedWeaponVisuals()
        {
            // Active weapon attached to RightHandWeaponSocket
            // Secondary weapon attached to holster
            // Handled dynamically based on active weapon slot
        }

        public void TriggerHitReaction()
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                animator.SetTrigger(HashHitTrigger);
            }
        }
    }
}
