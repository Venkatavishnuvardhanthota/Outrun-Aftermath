using System.Collections;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Audio;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.Vehicles;

namespace OutrunAftermath.Client.Runtime.Environment
{
    public enum HazardType
    {
        ExplosiveBarrel,
        ElectrifiedBarrier,
        DestructibleCover
    }

    /// <summary>
    /// Interactive environmental hazard for tactical maps in OUTRUN: AFTERMATH.
    /// Handles explosive chain reactions, electrified perimeter zones, and destructible cover degradation.
    /// </summary>
    public sealed class EnvironmentalHazard : MonoBehaviour, IDamageable
    {
        [Header("Hazard Configuration")]
        [SerializeField] private HazardType hazardType = HazardType.ExplosiveBarrel;
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float explosionRadius = 7.0f;
        [SerializeField] private float explosionDamage = 135f;
        [SerializeField] private float explosionForce = 750f;

        private float _currentHealth;
        private bool _isDestroyed;

        public HazardType Type => hazardType;
        public bool IsDestroyed => _isDestroyed;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isHeadshot)
        {
            TakeDamage(amount, hitPoint, -hitNormal);
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_isDestroyed) return;

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                TriggerDestruction();
            }
        }

        private void TriggerDestruction()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            switch (hazardType)
            {
                case HazardType.ExplosiveBarrel:
                    DetonateExplosion();
                    break;
                case HazardType.DestructibleCover:
                    DestroyCover();
                    break;
                case HazardType.ElectrifiedBarrier:
                    DeactivateBarrier();
                    break;
            }
        }

        private void DetonateExplosion()
        {
            Vector3 pos = transform.position;
            Debug.Log($"[EnvironmentalHazard] Explosive Barrel Detonated at {pos}!");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayExplosion(pos, 1.2f);
            }

            // Damage nearby players, vehicles, and other hazards (chain reactions)
            Collider[] colliders = Physics.OverlapSphere(pos, explosionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var hit = colliders[i];
                if (hit.gameObject == gameObject) continue;

                float distance = Vector3.Distance(pos, hit.transform.position);
                float damageFalloff = Mathf.Clamp01(1f - (distance / explosionRadius));
                float finalDmg = explosionDamage * damageFalloff;

                // Damage player health
                var health = hit.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    health.TakeDamage(finalDmg, pos);
                }

                // Damage vehicle components
                var vehicle = hit.GetComponentInParent<NetworkVehicleController>();
                if (vehicle != null)
                {
                    vehicle.ApplyExplosiveDamageServer(finalDmg);
                }

                // Chain-detonate other explosive barrels
                var otherHazard = hit.GetComponentInParent<EnvironmentalHazard>();
                if (otherHazard != null && !otherHazard.IsDestroyed)
                {
                    otherHazard.TakeDamage(explosionDamage, pos, (hit.transform.position - pos).normalized);
                }

                // Physics impulse
                var rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    rb.AddExplosionForce(explosionForce, pos, explosionRadius, 1.5f, ForceMode.Impulse);
                }
            }

            // Visual explosion flash
            SpawnExplosionVisuals(pos);

            Destroy(gameObject, 0.05f);
        }

        private void DestroyCover()
        {
            Debug.Log($"[EnvironmentalHazard] Cover collapsed at {transform.position}");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFootstep(transform.position, "metal", 1.0f);
            }
            Destroy(gameObject);
        }

        private void DeactivateBarrier()
        {
            Debug.Log($"[EnvironmentalHazard] Barrier deactivated at {transform.position}");
            Destroy(gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isDestroyed || hazardType != HazardType.ElectrifiedBarrier) return;

            // Electrified hazard continuous DPS
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(35f * Time.deltaTime, other.transform.position);
            }
        }

        private void SpawnExplosionVisuals(Vector3 pos)
        {
            var vfxGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vfxGo.name = "ExplosionFlash";
            vfxGo.transform.position = pos;
            vfxGo.transform.localScale = Vector3.one * 3.5f;

            var col = vfxGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.45f, 0.05f, 1f);
            vfxGo.GetComponent<Renderer>().material = mat;

            Destroy(vfxGo, 0.15f);
        }
    }
}
