using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class DrunkSystem : NetworkBehaviour
{
    [Header("Drunk Level")]
    [SerializeField] private float maxDrunkLevel = 100f;
    [SerializeField] private float drunkPerBottle = 25f;
    [SerializeField] private float soberRate = 4f;

    [Header("Penalty Curves (X = DrunkRatio 0-1)")]
    [SerializeField] private AnimationCurve spreadPenaltyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 18f);
    [SerializeField] private AnimationCurve swayMultCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 5f);
    [SerializeField] private AnimationCurve movementPenalty = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);
    [SerializeField] private AnimationCurve reloadTimeMultCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 2.5f);

    public UnityEvent<float> OnDrunkLevelChanged;
    public UnityEvent OnSober;
    public UnityEvent OnMaxDrunk;

    [Networked] public float CurrentDrunkLevel { get; private set; }

    private float _lastRatio;

    public override void Spawned()
    {
        if (HasStateAuthority)
            CurrentDrunkLevel = 0f;

        _lastRatio = GetDrunkRatio();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (CurrentDrunkLevel <= 0f) return;

        CurrentDrunkLevel = Mathf.Max(0f, CurrentDrunkLevel - soberRate * Runner.DeltaTime);
    }

    public override void Render()
    {
        float ratio = GetDrunkRatio();
        if (!Mathf.Approximately(ratio, _lastRatio))
        {
            OnDrunkLevelChanged?.Invoke(ratio);
            if (Mathf.Approximately(ratio, 0f))
                OnSober?.Invoke();
            _lastRatio = ratio;
        }
    }

    public void CollectBottle()
    {
        if (!HasStateAuthority) return;

        CurrentDrunkLevel = Mathf.Min(CurrentDrunkLevel + drunkPerBottle, maxDrunkLevel);
        if (Mathf.Approximately(CurrentDrunkLevel, maxDrunkLevel))
            OnMaxDrunk?.Invoke();
    }

    public void AddDrunkLevel(float amount)
    {
        if (!HasStateAuthority) return;
        CurrentDrunkLevel = Mathf.Clamp(CurrentDrunkLevel + amount, 0f, maxDrunkLevel);
        if (Mathf.Approximately(CurrentDrunkLevel, maxDrunkLevel))
            OnMaxDrunk?.Invoke();
    }

    public void ResetDrunk()
    {
        if (!HasStateAuthority) return;
        CurrentDrunkLevel = 0f;
    }

    public float GetDrunkRatio() => maxDrunkLevel > 0f ? CurrentDrunkLevel / maxDrunkLevel : 0f;
    public float GetSpreadPenalty() => spreadPenaltyCurve.Evaluate(GetDrunkRatio());
    public float GetSwayMultiplier() => swayMultCurve.Evaluate(GetDrunkRatio());
    public float GetMovementPenalty() => movementPenalty.Evaluate(GetDrunkRatio());
    public float GetReloadTimeMult() => reloadTimeMultCurve.Evaluate(GetDrunkRatio());
}