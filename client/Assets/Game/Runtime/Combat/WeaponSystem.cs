using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Input;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Runtime.Character;

namespace OutrunAftermath.Client.Runtime.Combat
{
    public enum FireMode
    {
        FullAuto,
        SemiAuto,
        Burst
    }

    [System.Serializable]
    public sealed class WeaponData
    {
        public string id;
        public string displayName;
        public FireMode fireMode;
        public float damage = 28f;
        public float headshotMultiplier = 2.0f;
        public float roundsPerMinute = 650f;
        public int magazineSize = 30;
        public int currentMagazine = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 2.2f;
        public float range = 85f;
        public float baseSpread = 1.2f;
        public float maxSpread = 6.5f;
        public float recoilPitch = 1.1f;
        public float recoilYaw = 0.45f;
        public int pelletsPerShot = 1; // 8 for shotguns
    }

    /// <summary>
    /// Tactical Weapon System for OUTRUN: AFTERMATH.
    /// Supports multiple weapon slots, ADS accuracy bonuses, dynamic spread bloom,
    /// procedural recoil impulse, hit registration, and realistic ammo lifecycle.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour
    {
        [Header("Equipped Arsenal")]
        [SerializeField] private List<WeaponData> loadout = new List<WeaponData>();
        [SerializeField] private int activeSlotIndex = 0;

        [Header("Gun Rig & Visuals")]
        [SerializeField] private Transform gunRoot;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private LayerMask hitMask = ~0;

        private CameraRig _cameraRig;
        private CharacterMotor _motor;
        private PlayerHealth _health;

        private float _nextFireTime;
        private float _currentSpread;
        private bool _isReloading;
        private float _reloadTimer;
        private Vector3 _gunLocalRestPos = new Vector3(0.24f, -0.2f, 0.45f);
        private Vector3 _gunAdsPos = new Vector3(0f, -0.14f, 0.32f);
        private Vector3 _proceduralRecoil;

        private readonly Dictionary<int, GameObject> _weaponVisualInstances = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Transform> _weaponMuzzlePoints = new Dictionary<int, Transform>();

        public event Action<WeaponData> OnWeaponSwitched;
        public event Action<int, int> OnAmmoChanged; // currentMag, reserve
        public event Action<bool, bool> OnHitConfirmed; // isHit, isHeadshot (kill confirmation)
        public event Action OnFired;
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;
        public event Action<Ray, float, int> OnNetworkFireRequested; // baseRay, spreadDeg, slotIndex

        public bool IsNetworkManaged { get; set; }
        
        private static bool _strictVisualValidationMode = false;
        public static bool StrictVisualValidationMode
        {
            get => _strictVisualValidationMode;
            set
            {
                _strictVisualValidationMode = value;
                if (value)
                {
                    if (!ValidateAllWeaponPrefabs(out string reason))
                    {
                        Debug.LogError($"[WeaponSystem] Strict mode weapon validation failed: {reason}");
                        throw new System.IO.FileNotFoundException($"[WeaponSystem] Strict mode weapon validation failed: {reason}");
                    }
                }
            }
        }

        public static readonly string[] RequiredWeaponPrefabs = new string[]
        {
            "PF_Weapon_Vanguard_AssaultRifle",
            "PF_Weapon_Breacher_Shotgun",
            "PF_Weapon_Sidekick_Pistol"
        };

        public static bool ValidateAllWeaponPrefabs(out string failureReason)
        {
            foreach (var prefabName in RequiredWeaponPrefabs)
            {
                var prefab = Resources.Load<GameObject>($"Art/Prefabs/{prefabName}");
                if (prefab == null)
                {
                    failureReason = $"Missing weapon prefab in Resources: 'Art/Prefabs/{prefabName}'";
                    return false;
                }

                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    failureReason = $"Weapon prefab '{prefabName}' is missing a valid MeshFilter or Mesh.";
                    return false;
                }

                var mr = prefab.GetComponentInChildren<Renderer>();
                if (mr == null || mr.sharedMaterial == null)
                {
                    failureReason = $"Weapon prefab '{prefabName}' is missing a valid Renderer or Material.";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        public IReadOnlyList<WeaponData> Loadout => loadout;
        public int ActiveSlotIndex => activeSlotIndex;
        public WeaponData CurrentWeapon => (loadout.Count > 0 && activeSlotIndex < loadout.Count) ? loadout[activeSlotIndex] : null;
        public WeaponData ActiveWeapon => CurrentWeapon;
        public bool IsReloading => _isReloading;
        public float ReloadProgress => _isReloading && CurrentWeapon != null ? (1f - (_reloadTimer / CurrentWeapon.reloadTime)) : 0f;
        public float CurrentSpread => _currentSpread;
        public Vector3 MuzzleWorldPosition => muzzlePoint != null ? muzzlePoint.position : (_cameraRig != null && _cameraRig.UnityCamera != null ? _cameraRig.UnityCamera.transform.position : transform.position + Vector3.up * 1.5f);

        private void Awake()
        {
            EnsureDefaultLoadout();

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-strict-assets", StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals("-strict-weapons", StringComparison.OrdinalIgnoreCase))
                {
                    StrictVisualValidationMode = true;
                    Debug.Log("[WeaponSystem] Strict Visual Validation Mode ENABLED via command line flag.");
                }
            }

            // Actively validate weapon prefabs on startup
            bool valid = ValidateAllWeaponPrefabs(out string failureReason);
            if (valid)
            {
                Debug.Log("[WeaponSystem] All required weapon prefabs validated successfully in Resources.");
            }
            else
            {
                string warnMsg = $"[WeaponSystem] Weapon prefab validation discrepancy: {failureReason}";
                if (StrictVisualValidationMode)
                {
                    Debug.LogError($"[WeaponSystem] STRICT VALIDATION FAILURE: {warnMsg}");
                    throw new System.IO.FileNotFoundException(warnMsg);
                }
                else
                {
                    Debug.LogWarning(warnMsg);
                }
            }
        }

        public void Initialize(CameraRig cameraRig, CharacterMotor motor, PlayerHealth health)
        {
            _cameraRig = cameraRig;
            _motor = motor;
            _health = health;

            EnsureDefaultLoadout();

            if (!ValidateAllWeaponPrefabs(out string err) && StrictVisualValidationMode)
            {
                throw new System.IO.FileNotFoundException($"[WeaponSystem] Cannot initialize in strict mode: {err}");
            }

            SetupVisualGunRig();
            EquipSlot(0);
        }

        public void EnsureDefaultLoadout()
        {
            if (loadout.Count == 0)
            {
                // Default 3-weapon tactical loadout
                loadout.Add(new WeaponData
                {
                    id = "vanguard4",
                    displayName = "Vanguard-4 Assault Rifle",
                    fireMode = FireMode.FullAuto,
                    damage = 28f,
                    headshotMultiplier = 2.0f,
                    roundsPerMinute = 620f,
                    magazineSize = 30,
                    currentMagazine = 30,
                    reserveAmmo = 150,
                    reloadTime = 2.2f,
                    range = 100f,
                    baseSpread = 1.1f,
                    maxSpread = 5.2f,
                    recoilPitch = 1.15f,
                    recoilYaw = 0.4f,
                    pelletsPerShot = 1
                });

                loadout.Add(new WeaponData
                {
                    id = "breacher12",
                    displayName = "Breacher-12 Tactical Shotgun",
                    fireMode = FireMode.SemiAuto,
                    damage = 16f, // 16 * 8 = 128 point blank
                    headshotMultiplier = 1.5f,
                    roundsPerMinute = 160f,
                    magazineSize = 8,
                    currentMagazine = 8,
                    reserveAmmo = 40,
                    reloadTime = 2.8f,
                    range = 35f,
                    baseSpread = 3.8f,
                    maxSpread = 7.5f,
                    recoilPitch = 3.2f,
                    recoilYaw = 0.8f,
                    pelletsPerShot = 8
                });

                loadout.Add(new WeaponData
                {
                    id = "sidekick9",
                    displayName = "Sidekick-9 Combat Pistol",
                    fireMode = FireMode.SemiAuto,
                    damage = 34f,
                    headshotMultiplier = 2.0f,
                    roundsPerMinute = 440f,
                    magazineSize = 15,
                    currentMagazine = 15,
                    reserveAmmo = 75,
                    reloadTime = 1.6f,
                    range = 65f,
                    baseSpread = 0.8f,
                    maxSpread = 4.0f,
                    recoilPitch = 0.85f,
                    recoilYaw = 0.3f,
                    pelletsPerShot = 1
                });
            }
        }

        private void SetupVisualGunRig()
        {
            if (gunRoot == null && _cameraRig != null && _cameraRig.UnityCamera != null)
            {
                var gunHolder = new GameObject("GunRig");
                gunHolder.transform.SetParent(_cameraRig.UnityCamera.transform);
                gunHolder.transform.localPosition = _gunLocalRestPos;
                gunHolder.transform.localRotation = Quaternion.identity;
                gunRoot = gunHolder.transform;

                UpdateActiveWeaponVisual(activeSlotIndex);
            }
        }

        private void UpdateActiveWeaponVisual(int slotIndex)
        {
            if (gunRoot == null) return;
            if (slotIndex < 0 || slotIndex >= loadout.Count) return;

            var weapon = loadout[slotIndex];
            string weaponId = (weapon.id ?? "vanguard4").ToLowerInvariant();

            // Set weapon-specific rest and ADS positions
            switch (weaponId)
            {
                case "breacher12":
                    _gunLocalRestPos = new Vector3(0.25f, -0.22f, 0.44f);
                    _gunAdsPos = new Vector3(0f, -0.15f, 0.30f);
                    break;
                case "sidekick9":
                    _gunLocalRestPos = new Vector3(0.20f, -0.20f, 0.38f);
                    _gunAdsPos = new Vector3(0f, -0.13f, 0.28f);
                    break;
                case "vanguard4":
                default:
                    _gunLocalRestPos = new Vector3(0.24f, -0.2f, 0.45f);
                    _gunAdsPos = new Vector3(0f, -0.14f, 0.32f);
                    break;
            }

            // Deactivate all previously loaded weapon visuals
            foreach (var kvp in _weaponVisualInstances)
            {
                if (kvp.Value != null) kvp.Value.SetActive(false);
            }

            // Check if visual is already cached
            if (!_weaponVisualInstances.TryGetValue(slotIndex, out var visualGo) || visualGo == null)
            {
                string prefabName = weaponId switch
                {
                    "breacher12" => "PF_Weapon_Breacher_Shotgun",
                    "sidekick9" => "PF_Weapon_Sidekick_Pistol",
                    _ => "PF_Weapon_Vanguard_AssaultRifle"
                };

                Vector3 localOffset = weaponId switch
                {
                    "breacher12" => new Vector3(0f, -0.05f, 0.24f),
                    "sidekick9" => new Vector3(0f, -0.04f, 0.18f),
                    _ => new Vector3(0f, -0.04f, 0.22f)
                };

                Vector3 muzzleOffset = weaponId switch
                {
                    "breacher12" => new Vector3(0f, 0.021f, 0.54f),
                    "sidekick9" => new Vector3(0f, 0.021f, 0.19f),
                    _ => new Vector3(0f, 0.015f, 0.49f)
                };

                var prefab = Resources.Load<GameObject>($"Art/Prefabs/{prefabName}");
                if (prefab != null)
                {
                    visualGo = Instantiate(prefab, gunRoot);
                    visualGo.name = $"{prefabName}_Visual";
                    visualGo.transform.localPosition = localOffset;
                    visualGo.transform.localRotation = Quaternion.identity;
                    visualGo.transform.localScale = Vector3.one;

                    var muzzle = new GameObject("MuzzleTip");
                    muzzle.transform.SetParent(visualGo.transform);
                    muzzle.transform.localPosition = muzzleOffset;

                    _weaponVisualInstances[slotIndex] = visualGo;
                    _weaponMuzzlePoints[slotIndex] = muzzle.transform;
                }
                else
                {
                    if (StrictVisualValidationMode)
                    {
                        string errMsg = $"[WeaponSystem][STRICT ASSET FAILURE] Required weapon visual prefab 'Art/Prefabs/{prefabName}' for weapon '{weaponId}' was not found in Resources! Primitive fallbacks are strictly prohibited in production and strict validation mode.";
                        Debug.LogError(errMsg);
                        throw new System.IO.FileNotFoundException(errMsg);
                    }

                    Debug.LogWarning($"[WeaponSystem][DEV FALLBACK WARNING] Missing production weapon prefab 'Art/Prefabs/{prefabName}' for weapon '{weaponId}'. Primitive blockout geometry will ONLY be rendered in development mode.");
                    visualGo = CreateDevFallbackGunVisual(slotIndex, weaponId, out var muzzle);
                    _weaponVisualInstances[slotIndex] = visualGo;
                    _weaponMuzzlePoints[slotIndex] = muzzle;
                }
            }

            if (visualGo != null)
            {
                visualGo.SetActive(true);
            }

            if (_weaponMuzzlePoints.TryGetValue(slotIndex, out var activeMuz) && activeMuz != null)
            {
                muzzlePoint = activeMuz;
            }
        }

        /// <summary>
        /// DEVELOPMENT-ONLY fallback gun visual.
        /// Throws in strict validation mode. Never executed in production builds.
        /// Visibly tagged with [DEV_FALLBACK] banner in hierarchy.
        /// </summary>
        private GameObject CreateDevFallbackGunVisual(int slotIndex, string weaponId, out Transform muzzle)
        {
            if (StrictVisualValidationMode)
            {
                throw new System.InvalidOperationException($"[WeaponSystem] Attempted to create primitive fallback for '{weaponId}' while StrictVisualValidationMode is active.");
            }

            var fallbackRoot = new GameObject($"[DEV_FALLBACK]_{weaponId}_Visual");
            fallbackRoot.transform.SetParent(gunRoot);
            fallbackRoot.transform.localPosition = Vector3.zero;
            fallbackRoot.transform.localRotation = Quaternion.identity;

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var gunMat = new Material(litShader) { color = new Color(0.85f, 0.25f, 0.25f) };

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "DEV_FALLBACK_Body";
            body.transform.SetParent(fallbackRoot.transform);
            body.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            body.transform.localScale = new Vector3(0.06f, 0.10f, 0.35f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            body.GetComponent<Renderer>().material = gunMat;

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "DEV_FALLBACK_Barrel";
            barrel.transform.SetParent(fallbackRoot.transform);
            barrel.transform.localPosition = new Vector3(0f, 0.02f, 0.38f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.03f, 0.12f, 0.03f);
            var bCol = barrel.GetComponent<Collider>();
            if (bCol != null) Destroy(bCol);
            barrel.GetComponent<Renderer>().material = gunMat;

            var mGo = new GameObject("MuzzleTip");
            mGo.transform.SetParent(fallbackRoot.transform);
            mGo.transform.localPosition = new Vector3(0f, 0.02f, 0.50f);
            muzzle = mGo.transform;

            return fallbackRoot;
        }

        public void ProcessWeapons(PlayerInputState input)
        {
            if (_health != null && _health.State != PlayerLifeState.Alive)
            {
                return; // Disabled when downed or eliminated
            }

            var weapon = CurrentWeapon;
            if (weapon == null) return;

            // Weapon slot switching (1, 2, 3)
            if (input.WeaponSlot > 0 && input.WeaponSlot <= loadout.Count)
            {
                EquipSlot(input.WeaponSlot - 1);
                return;
            }

            // Reload logic
            if (_isReloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f)
                {
                    CompleteReload();
                }
            }
            else if (input.Reload)
            {
                StartReload();
            }

            // Spread dynamics
            UpdateSpread(input);

            // Firing logic
            bool wantsToFire = (weapon.fireMode == FireMode.FullAuto) ? input.FireHeld : input.Fire;

            if (wantsToFire && Time.time >= _nextFireTime && !_isReloading)
            {
                if (weapon.currentMagazine > 0)
                {
                    ExecuteShot(weapon, input.Aim);
                }
                else
                {
                    // Empty mag click
                    _nextFireTime = Time.time + 0.35f;
                    if (Audio.AudioManager.Instance != null)
                    {
                        Audio.AudioManager.Instance.PlayReloadClick(transform.position, 0, 0.4f);
                    }
                    StartReload();
                }
            }

            // Procedural gun animation (ADS alignment and recoil recovery)
            UpdateGunVisuals(input.Aim);
        }

        private void UpdateSpread(PlayerInputState input)
        {
            var weapon = CurrentWeapon;
            if (weapon == null) return;

            float targetSpread = weapon.baseSpread;

            // ADS tightens spread significantly
            if (input.Aim) targetSpread *= 0.35f;

            // Stance modifier
            if (_motor != null)
            {
                if (_motor.Stance == MovementStance.Crouching) targetSpread *= 0.7f;
                else if (_motor.Stance == MovementStance.Sliding || !_motor.IsGrounded) targetSpread *= 2.4f;
                else if (input.Sprint) targetSpread *= 1.8f;
                else if (_motor.CurrentSpeed > 0.5f) targetSpread *= 1.4f;
            }

            // Smooth recovery towards target spread
            _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * 8f);
        }

        private void ExecuteShot(WeaponData weapon, bool isAiming)
        {
            weapon.currentMagazine--;
            _nextFireTime = Time.time + (60f / weapon.roundsPerMinute);

            // Increase spread per shot
            _currentSpread = Mathf.Min(weapon.maxSpread, _currentSpread + (weapon.baseSpread * 0.45f));

            // Camera recoil
            if (_cameraRig != null)
            {
                float aimMultiplier = isAiming ? 0.65f : 1.0f;
                _cameraRig.ApplyRecoilImpulse(weapon.recoilPitch * aimMultiplier, weapon.recoilYaw * aimMultiplier);
            }

            // Procedural gun kick
            _proceduralRecoil += new Vector3(
                UnityEngine.Random.Range(-0.015f, 0.015f),
                0.025f,
                -0.07f
            );

            // Procedural spatial gunshot audio
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayGunshot(transform.position, weapon.id, 1.0f);
            }

            // Raycast shot execution
            Ray baseRay = _cameraRig != null ? _cameraRig.GetAimRay() : new Ray(transform.position + Vector3.up * 1.6f, transform.forward);

            if (IsNetworkManaged)
            {
                OnNetworkFireRequested?.Invoke(baseRay, _currentSpread, activeSlotIndex);
                OnFired?.Invoke();
                OnAmmoChanged?.Invoke(weapon.currentMagazine, weapon.reserveAmmo);
                return;
            }

            bool registeredAnyHit = false;
            bool registeredHeadshot = false;

            for (int i = 0; i < weapon.pelletsPerShot; i++)
            {
                Vector3 spreadDir = CalculateSpreadDirection(baseRay.direction, _currentSpread);
                Ray shotRay = new Ray(baseRay.origin, spreadDir);

                if (TryRaycastExcludingSelf(shotRay, weapon.range, out RaycastHit hit))
                {
                    registeredAnyHit = true;

                    // Check headshot (e.g. hit height relative to target or collider name)
                    bool isHeadshot = hit.collider.name.ToLower().Contains("head") ||
                                      (hit.point.y - hit.collider.bounds.min.y) > (hit.collider.bounds.size.y * 0.75f);

                    if (isHeadshot) registeredHeadshot = true;

                    float actualDamage = isHeadshot ? weapon.damage * weapon.headshotMultiplier : weapon.damage;

                    // Falloff over distance
                    float distanceFactor = 1f - Mathf.Clamp01(hit.distance / weapon.range) * 0.45f;
                    actualDamage *= distanceFactor;

                    // Apply damage to target
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(actualDamage, hit.point, hit.normal, isHeadshot);
                    }

                    var playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                    if (playerHealth != null && playerHealth != _health)
                    {
                        playerHealth.TakeDamage(actualDamage, hit.point);
                    }

                    // Environmental Hazard damage
                    var hazard = hit.collider.GetComponentInParent<Environment.EnvironmentalHazard>();
                    if (hazard != null)
                    {
                        hazard.TakeDamage(actualDamage, hit.point, shotRay.direction);
                    }

                    var rb = hit.collider.attachedRigidbody;
                    if (rb != null)
                    {
                        rb.AddForceAtPosition(shotRay.direction * actualDamage * 4f, hit.point, ForceMode.Impulse);
                    }

                    // Render bullet tracer
                    StartCoroutine(RenderTracer(MuzzleWorldPosition, hit.point));
                }
                else
                {
                    // Bullet tracer into the distance
                    Vector3 endPoint = shotRay.origin + shotRay.direction * weapon.range;
                    StartCoroutine(RenderTracer(MuzzleWorldPosition, endPoint));
                }
            }

            if (registeredAnyHit)
            {
                OnHitConfirmed?.Invoke(true, registeredHeadshot);
                if (Audio.AudioManager.Instance != null)
                {
                    Audio.AudioManager.Instance.PlayHitMarker(registeredHeadshot, false);
                }
            }

            OnFired?.Invoke();
            OnAmmoChanged?.Invoke(weapon.currentMagazine, weapon.reserveAmmo);
        }

        public void TriggerRemoteHitConfirm(bool isHit, bool isHeadshot)
        {
            if (isHit)
            {
                OnHitConfirmed?.Invoke(true, isHeadshot);
                if (Audio.AudioManager.Instance != null)
                {
                    Audio.AudioManager.Instance.PlayHitMarker(isHeadshot, false);
                }
            }
        }

        public void SpawnVisualTracer(Vector3 start, Vector3 end)
        {
            StartCoroutine(RenderTracer(start, end));
        }

        public void SetGunRigVisible(bool visible)
        {
            if (gunRoot != null)
            {
                gunRoot.gameObject.SetActive(visible);
            }
        }

        private bool TryRaycastExcludingSelf(Ray ray, float maxDist, out RaycastHit closestHit)
        {
            var hits = Physics.RaycastAll(ray, maxDist, hitMask, QueryTriggerInteraction.Ignore);
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

        private Vector3 CalculateSpreadDirection(Vector3 forward, float spreadAngleDegrees)
        {
            float spreadRadius = Mathf.Tan(spreadAngleDegrees * Mathf.Deg2Rad);
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spreadRadius;

            Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, forward).normalized;

            return (forward + right * randomCircle.x + up * randomCircle.y).normalized;
        }

        private IEnumerator RenderTracer(Vector3 start, Vector3 end)
        {
            var lineObj = new GameObject("BulletTracer");
            var line = lineObj.AddComponent<LineRenderer>();
            line.startWidth = 0.035f;
            line.endWidth = 0.015f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? 
                         Shader.Find("Sprites/Default") ?? 
                         Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.85f, 0.35f, 0.85f);
                line.material = mat;
            }

            yield return new WaitForSeconds(0.045f);
            Destroy(lineObj);
        }

        public void StartReload()
        {
            var weapon = CurrentWeapon;
            if (weapon == null || _isReloading) return;
            if (weapon.currentMagazine >= weapon.magazineSize || weapon.reserveAmmo <= 0) return;

            _isReloading = true;
            _reloadTimer = weapon.reloadTime;
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayReloadClick(transform.position, 1, 0.7f);
            }
            OnReloadStarted?.Invoke();
        }

        private void CompleteReload()
        {
            var weapon = CurrentWeapon;
            if (weapon != null)
            {
                int needed = weapon.magazineSize - weapon.currentMagazine;
                int transfer = Mathf.Min(needed, weapon.reserveAmmo);
                weapon.currentMagazine += transfer;
                weapon.reserveAmmo -= transfer;

                OnAmmoChanged?.Invoke(weapon.currentMagazine, weapon.reserveAmmo);
            }

            _isReloading = false;
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayReloadClick(transform.position, 2, 0.8f);
            }
            OnReloadCompleted?.Invoke();
        }

        public void EquipSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= loadout.Count) return;

            activeSlotIndex = slotIndex;
            _isReloading = false;
            _currentSpread = CurrentWeapon.baseSpread;

            UpdateActiveWeaponVisual(slotIndex);

            OnWeaponSwitched?.Invoke(CurrentWeapon);
            OnAmmoChanged?.Invoke(CurrentWeapon.currentMagazine, CurrentWeapon.reserveAmmo);
        }

        private void UpdateGunVisuals(bool isAiming)
        {
            if (gunRoot == null) return;

            Vector3 targetLocalPos = isAiming ? _gunAdsPos : _gunLocalRestPos;
            targetLocalPos += _proceduralRecoil;

            gunRoot.localPosition = Vector3.Lerp(gunRoot.localPosition, targetLocalPos, Time.deltaTime * 18f);
            gunRoot.localRotation = Quaternion.Slerp(gunRoot.localRotation, Quaternion.identity, Time.deltaTime * 14f);

            // Recoil recovery
            _proceduralRecoil = Vector3.Lerp(_proceduralRecoil, Vector3.zero, Time.deltaTime * 12f);
        }
    }
}
