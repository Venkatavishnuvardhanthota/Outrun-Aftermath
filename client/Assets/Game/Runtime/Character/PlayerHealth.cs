using System;
using UnityEngine;

namespace OutrunAftermath.Client.Runtime.Character
{
    public enum PlayerLifeState
    {
        Alive,
        Downed,
        BeingRevived,
        Eliminated
    }

    /// <summary>
    /// Player lifecycle and damage receiver for OUTRUN: AFTERMATH.
    /// Manages health, tactical armor, downed state, bleed-out, revival, and elimination.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health & Armor")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxArmor = 50f;
        [SerializeField] private float currentHealth;
        [SerializeField] private float currentArmor;

        [Header("Downed & Revive Config")]
        [SerializeField] private float bleedOutDuration = 30f;
        [SerializeField] private float downedHealth = 60f; // Armor to absorb bullets while downed
        [SerializeField] private float reviveHoldDuration = 4.0f;
        [SerializeField] private float reviveHealHealth = 40f;
        [SerializeField] private bool allowRespawn = true;
        [SerializeField] private float respawnDelay = 3.5f;

        private PlayerLifeState _state = PlayerLifeState.Alive;
        private float _bleedOutTimer;
        private float _currentDownedHealth;
        private float _reviveProgress;
        private float _respawnTimer;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public event Action<float, float> OnHealthArmorChanged; // currentHealth, currentArmor
        public event Action<float, Vector3> OnDamaged; // damageAmount, hitPoint
        public event Action OnDowned;
        public event Action OnRevived;
        public event Action OnEliminated;

        public PlayerLifeState State => _state;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentArmor => currentArmor;
        public float MaxArmor => maxArmor;
        public float BleedOutRemaining => _bleedOutTimer;
        public float ReviveProgress => _reviveProgress;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentArmor = maxArmor;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (_state == PlayerLifeState.Downed || _state == PlayerLifeState.BeingRevived)
            {
                _bleedOutTimer -= Time.deltaTime;
                if (_bleedOutTimer <= 0f)
                {
                    Eliminate();
                }
            }
            else if (_state == PlayerLifeState.Eliminated && allowRespawn)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint = default)
        {
            if (_state == PlayerLifeState.Eliminated) return;

            OnDamaged?.Invoke(amount, hitPoint);

            if (_state == PlayerLifeState.Downed || _state == PlayerLifeState.BeingRevived)
            {
                // Damage while downed finishes the player
                _currentDownedHealth -= amount;
                _reviveProgress = 0f; // Damage interrupts revive
                if (_state == PlayerLifeState.BeingRevived)
                {
                    _state = PlayerLifeState.Downed;
                }

                if (_currentDownedHealth <= 0f)
                {
                    Eliminate();
                }
                return;
            }

            // Armor absorbs 65% of incoming damage
            if (currentArmor > 0f)
            {
                float armorDamage = amount * 0.65f;
                float healthDamage = amount * 0.35f;

                if (currentArmor >= armorDamage)
                {
                    currentArmor -= armorDamage;
                    currentHealth -= healthDamage;
                }
                else
                {
                    float remainingArmor = currentArmor;
                    currentArmor = 0f;
                    float overflowDamage = (armorDamage - remainingArmor) / 0.65f;
                    currentHealth -= (healthDamage + overflowDamage);
                }
            }
            else
            {
                currentHealth -= amount;
            }

            currentHealth = Mathf.Max(0f, currentHealth);
            OnHealthArmorChanged?.Invoke(currentHealth, currentArmor);

            if (currentHealth <= 0f)
            {
                DownPlayer();
            }
        }

        public void DownPlayer()
        {
            if (_state == PlayerLifeState.Downed || _state == PlayerLifeState.Eliminated) return;

            _state = PlayerLifeState.Downed;
            _bleedOutTimer = bleedOutDuration;
            _currentDownedHealth = downedHealth;
            _reviveProgress = 0f;

            OnDowned?.Invoke();
        }

        public bool TryProgressRevive(float deltaTime)
        {
            if (_state != PlayerLifeState.Downed && _state != PlayerLifeState.BeingRevived) return false;

            _state = PlayerLifeState.BeingRevived;
            _reviveProgress += deltaTime / reviveHoldDuration;

            if (_reviveProgress >= 1.0f)
            {
                RevivePlayer();
                return true;
            }
            return false;
        }

        public void CancelRevive()
        {
            if (_state == PlayerLifeState.BeingRevived)
            {
                _state = PlayerLifeState.Downed;
                _reviveProgress = 0f;
            }
        }

        public void RevivePlayer()
        {
            _state = PlayerLifeState.Alive;
            currentHealth = reviveHealHealth;
            currentArmor = 0f; // Starts without armor after revive
            _reviveProgress = 0f;

            OnHealthArmorChanged?.Invoke(currentHealth, currentArmor);
            OnRevived?.Invoke();
        }

        public void Eliminate()
        {
            if (_state == PlayerLifeState.Eliminated) return;

            _state = PlayerLifeState.Eliminated;
            currentHealth = 0f;
            currentArmor = 0f;
            _respawnTimer = respawnDelay;

            OnHealthArmorChanged?.Invoke(0f, 0f);
            OnEliminated?.Invoke();
        }

        public void Respawn()
        {
            _state = PlayerLifeState.Alive;
            currentHealth = maxHealth;
            currentArmor = maxArmor;
            _reviveProgress = 0f;

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;

            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = _spawnPosition;
                cc.enabled = true;
            }

            OnHealthArmorChanged?.Invoke(currentHealth, currentArmor);
            OnRevived?.Invoke();
        }

        public void SetSpawnPoint(Vector3 pos, Quaternion rot)
        {
            _spawnPosition = pos;
            _spawnRotation = rot;
        }

        public void Heal(float amount)
        {
            if (_state != PlayerLifeState.Alive) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthArmorChanged?.Invoke(currentHealth, currentArmor);
        }

        public void RepairArmor(float amount)
        {
            if (_state != PlayerLifeState.Alive) return;
            currentArmor = Mathf.Min(maxArmor, currentArmor + amount);
            OnHealthArmorChanged?.Invoke(currentHealth, currentArmor);
        }
    }
}
