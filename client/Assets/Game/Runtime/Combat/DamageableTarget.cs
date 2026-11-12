using System;
using UnityEngine;

namespace OutrunAftermath.Client.Runtime.Combat
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isHeadshot);
    }

    /// <summary>
    /// Interactive combat target (training dummy, explosive barrel, combat drone).
    /// Provides hit feedback, headshot multipliers, physics impulses, and respawn.
    /// </summary>
    public sealed class DamageableTarget : MonoBehaviour, IDamageable
    {
        public enum TargetType
        {
            StandardDummy,
            ArmoredTarget,
            ExplosiveBarrel,
            TacticalDrone
        }

        [Header("Target Configuration")]
        [SerializeField] private TargetType type = TargetType.StandardDummy;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private float respawnDelay = 3f;

        [Header("Explosive Barrel Settings")]
        [SerializeField] private float explosionRadius = 6.5f;
        [SerializeField] private float explosionDamage = 135f;

        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Collider _collider;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _respawnTimer;
        private bool _isDestroyed;

        public event Action<float, bool> OnDamaged; // damage, isHeadshot
        public event Action OnDestroyed;

        public TargetType Type => type;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDestroyed => _isDestroyed;

        private void Awake()
        {
            currentHealth = maxHealth;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _collider = GetComponent<Collider>();

            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].material != null)
                {
                    _originalColors[i] = _renderers[i].material.color;
                }
            }
        }

        private void Update()
        {
            if (_isDestroyed && autoRespawn)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isHeadshot)
        {
            if (_isDestroyed) return;

            float actualDamage = isHeadshot ? amount * 1.85f : amount;
            currentHealth -= actualDamage;

            StartCoroutine(FlashHit());

            OnDamaged?.Invoke(actualDamage, isHeadshot);

            if (currentHealth <= 0f)
            {
                DestroyTarget(hitPoint, hitNormal);
            }
        }

        private System.Collections.IEnumerator FlashHit()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    _renderers[i].material.color = Color.white;
                }
            }

            yield return new WaitForSeconds(0.08f);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    _renderers[i].material.color = _originalColors[i];
                }
            }
        }

        private void DestroyTarget(Vector3 hitPoint, Vector3 hitNormal)
        {
            _isDestroyed = true;
            _respawnTimer = respawnDelay;
            OnDestroyed?.Invoke();

            if (type == TargetType.ExplosiveBarrel)
            {
                Explode();
            }

            // Hide mesh and disable collider
            if (_collider != null) _collider.enabled = false;
            foreach (var r in _renderers)
            {
                if (r != null) r.enabled = false;
            }
        }

        private void Explode()
        {
            // Detonate area damage
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                float damage = explosionDamage * falloff;

                var damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage, col.transform.position, (col.transform.position - transform.position).normalized, false);
                }

                var playerHealth = col.GetComponent<Character.PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, col.transform.position);
                }

                var rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(800f, transform.position, explosionRadius, 1.5f, ForceMode.Impulse);
                }
            }
        }

        private void Respawn()
        {
            _isDestroyed = false;
            currentHealth = maxHealth;
            transform.position = _startPosition;
            transform.rotation = _startRotation;

            if (_collider != null) _collider.enabled = true;
            foreach (var r in _renderers)
            {
                if (r != null) r.enabled = true;
            }
        }
    }
}
