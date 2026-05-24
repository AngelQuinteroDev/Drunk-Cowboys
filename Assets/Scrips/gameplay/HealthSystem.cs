using UnityEngine;
using UnityEngine.Events;

public class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnDelay = 3f;

    [Header("Drunk Damage Resistance")]
    [SerializeField] private AnimationCurve resistanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);

    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent OnDeath;
    public UnityEvent OnRespawn;

    public float CurrentHealth { get; private set; }
    public bool IsAlive { get; private set; }

    private DrunkSystem _drunk;

    private void Awake()
    {
        _drunk = GetComponent<DrunkSystem>();
        CurrentHealth = maxHealth;
        IsAlive = true;
    }

    public void TakeDamage(float rawDamage)
    {
        if (!IsAlive) return;

        float mult = _drunk != null ? resistanceCurve.Evaluate(_drunk.GetDrunkRatio()) : 1f;
        float damage = rawDamage * mult;

        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f) HandleDeath();
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public float GetHealthRatio() => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    private void HandleDeath()
    {
        IsAlive = false;
        OnDeath?.Invoke();
        if (canRespawn) StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        CurrentHealth = maxHealth;
        IsAlive = true;
        OnRespawn?.Invoke();
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}