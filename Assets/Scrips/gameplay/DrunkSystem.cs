using UnityEngine;
using UnityEngine.Events;

public class DrunkSystem : MonoBehaviour
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

    public float CurrentDrunkLevel { get; private set; }

    private void Awake()
    {
        CurrentDrunkLevel = 0f;
    }

    private void Update()
    {
        if (CurrentDrunkLevel <= 0f) return;

        CurrentDrunkLevel = Mathf.Max(0f, CurrentDrunkLevel - soberRate * Time.deltaTime);
        OnDrunkLevelChanged?.Invoke(GetDrunkRatio());

        if (CurrentDrunkLevel <= 0f) OnSober?.Invoke();
    }

    public void CollectBottle()
    {
        CurrentDrunkLevel = Mathf.Min(CurrentDrunkLevel + drunkPerBottle, maxDrunkLevel);
        OnDrunkLevelChanged?.Invoke(GetDrunkRatio());
        if (Mathf.Approximately(CurrentDrunkLevel, maxDrunkLevel)) OnMaxDrunk?.Invoke();
    }

    public void AddDrunkLevel(float amount)
    {
        CurrentDrunkLevel = Mathf.Clamp(CurrentDrunkLevel + amount, 0f, maxDrunkLevel);
        OnDrunkLevelChanged?.Invoke(GetDrunkRatio());
    }

    public float GetDrunkRatio() => maxDrunkLevel > 0f ? CurrentDrunkLevel / maxDrunkLevel : 0f;
    public float GetSpreadPenalty() => spreadPenaltyCurve.Evaluate(GetDrunkRatio());
    public float GetSwayMultiplier() => swayMultCurve.Evaluate(GetDrunkRatio());
    public float GetMovementPenalty() => movementPenalty.Evaluate(GetDrunkRatio());
    public float GetReloadTimeMult() => reloadTimeMultCurve.Evaluate(GetDrunkRatio());
}