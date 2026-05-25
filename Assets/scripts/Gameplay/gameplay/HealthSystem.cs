using Fusion;
using UnityEngine;
using UnityEngine.Events;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Gameplay
{
    public class HealthSystem : NetworkBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool canRespawn = true;
        [SerializeField] private float respawnDelay = 3f;

        [Header("Drunk Damage Resistance")]
        [SerializeField]
        private AnimationCurve resistanceCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 0.35f);

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip damageSound;

        public UnityEvent<float, float> OnHealthChanged;
        public UnityEvent OnDeath;
        public UnityEvent OnRespawn;

        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public bool IsAlive { get; private set; }
        [Networked] private TickTimer RespawnTimer { get; set; }

        private DrunkSystem _drunk;
        private ChangeDetector _changes;

        private float _lastHealth;
        private bool _lastAlive;

        public override void Spawned()
        {
            _drunk = GetComponent<DrunkSystem>();

            if (HasStateAuthority)
            {
                CurrentHealth = maxHealth;
                IsAlive = true;
            }

            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _lastHealth = CurrentHealth;
            _lastAlive = IsAlive;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (!IsAlive && canRespawn && RespawnTimer.Expired(Runner))
                ForceRespawn();
        }

        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                switch (change)
                {
                    case nameof(CurrentHealth):
                        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
                        _lastHealth = CurrentHealth;
                        break;

                    case nameof(IsAlive):
                        if (!IsAlive && _lastAlive)
                            OnDeath?.Invoke();

                        if (IsAlive && !_lastAlive)
                            OnRespawn?.Invoke();

                        _lastAlive = IsAlive;
                        break;
                }
            }
        }

        public void TakeDamage(float rawDamage) =>
            TakeDamage(rawDamage, PlayerRef.None);

        public void TakeDamage(float rawDamage, PlayerRef instigator)
        {
            if (!HasStateAuthority || !IsAlive) return;

            float mult = _drunk != null
                ? resistanceCurve.Evaluate(_drunk.GetDrunkRatio())
                : 1f;

            float damage = rawDamage * mult;

            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maxHealth);

            if (audioSource != null && damageSound != null)
                audioSource.PlayOneShot(damageSound);

            if (CurrentHealth <= 0f)
                HandleDeath(instigator);
        }

        public void Heal(float amount)
        {
            if (!HasStateAuthority || !IsAlive) return;

            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
        }

        public float GetHealthRatio() =>
            maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

        private void HandleDeath(PlayerRef instigator)
        {
            IsAlive = false;

            if (canRespawn)
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);

            if (TryGetComponent<PlayerController>(out var victim))
                victim.Deaths++;

            if (instigator != PlayerRef.None)
            {
                var killerObj = Runner.GetPlayerObject(instigator);

                if (killerObj != null &&
                    killerObj.TryGetComponent<PlayerController>(out var killer))
                {
                    killer.Kills++;
                }
            }

            if (Object != null && Object.InputAuthority != PlayerRef.None)
            {
                EventBus.Publish(new PlayerDied
                {
                    PlayerId = Object.InputAuthority.PlayerId,
                    KillerId = instigator != PlayerRef.None
                        ? instigator.PlayerId
                        : -1
                });
            }
        }

        public void ForceRespawn()
        {
            if (!HasStateAuthority) return;

            CurrentHealth = maxHealth;
            IsAlive = true;
            RespawnTimer = default;
        }
    }
}