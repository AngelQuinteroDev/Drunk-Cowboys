using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class DrunkSystem : NetworkBehaviour
{
    [Header("Drunk Level")]
    [SerializeField] private float maxDrunkLevel = 100f;
    [SerializeField] private float drunkPerBottle = 25f;
    [SerializeField] private float soberRate = 4f;

    [Header("Penalty Curves")]
    [SerializeField]
    private AnimationCurve spreadPenaltyCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 18f);

    [SerializeField]
    private AnimationCurve swayMultCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 5f);

    [SerializeField]
    private AnimationCurve movementPenalty =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);

    [SerializeField]
    private AnimationCurve reloadTimeMultCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 2.5f);

    [Header("Internal Audio")]
    [SerializeField] private AudioSource drunkLoopSource;
    [SerializeField] private AudioClip drunkLoop;
    [SerializeField] private float startDrunkAudioAt = 0.35f;

    public UnityEvent<float> OnDrunkLevelChanged;
    public UnityEvent OnSober;
    public UnityEvent OnMaxDrunk;

    [Networked]
    public float CurrentDrunkLevel { get; private set; }

    private float _lastRatio;

    public override void Spawned()
    {
        if (HasStateAuthority)
            CurrentDrunkLevel = 0f;

        _lastRatio = GetDrunkRatio();

        if (drunkLoopSource != null && drunkLoop != null)
        {
            drunkLoopSource.clip = drunkLoop;
            drunkLoopSource.loop = true;
            drunkLoopSource.spatialBlend = 0f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (CurrentDrunkLevel <= 0f) return;

        CurrentDrunkLevel =
            Mathf.Max(
                0f,
                CurrentDrunkLevel - soberRate * Runner.DeltaTime
            );
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

        UpdateInternalAudio(ratio);
    }

    private void UpdateInternalAudio(float ratio)
    {
        if (
            !HasInputAuthority ||
            drunkLoopSource == null ||
            drunkLoop == null
        )
            return;

        if (ratio >= startDrunkAudioAt)
        {
            if (!drunkLoopSource.isPlaying)
                drunkLoopSource.Play();

            drunkLoopSource.volume =
                Mathf.Lerp(
                    drunkLoopSource.volume,
                    ratio,
                    Time.deltaTime * 2f
                );

            drunkLoopSource.pitch =
                Mathf.Lerp(
                    drunkLoopSource.pitch,
                    0.85f + ratio * 0.25f,
                    Time.deltaTime * 2f
                );
        }
        else
        {
            drunkLoopSource.volume =
                Mathf.Lerp(
                    drunkLoopSource.volume,
                    0f,
                    Time.deltaTime * 3f
                );

            if (drunkLoopSource.volume <= 0.01f)
                drunkLoopSource.Stop();
        }
    }

    public void CollectBottle()
    {
        if (!HasStateAuthority) return;

        CurrentDrunkLevel =
            Mathf.Min(
                CurrentDrunkLevel + drunkPerBottle,
                maxDrunkLevel
            );

        if (Mathf.Approximately(CurrentDrunkLevel, maxDrunkLevel))
            OnMaxDrunk?.Invoke();
    }

    public void AddDrunkLevel(float amount)
    {
        if (!HasStateAuthority) return;

        CurrentDrunkLevel =
            Mathf.Clamp(
                CurrentDrunkLevel + amount,
                0f,
                maxDrunkLevel
            );

        if (Mathf.Approximately(CurrentDrunkLevel, maxDrunkLevel))
            OnMaxDrunk?.Invoke();
    }

    public void ResetDrunk()
    {
        if (!HasStateAuthority) return;

        CurrentDrunkLevel = 0f;

        if (drunkLoopSource != null)
            drunkLoopSource.Stop();
    }

    public float GetDrunkRatio() =>
        maxDrunkLevel > 0f
            ? CurrentDrunkLevel / maxDrunkLevel
            : 0f;

    public float GetSpreadPenalty() =>
        spreadPenaltyCurve.Evaluate(GetDrunkRatio());

    public float GetSwayMultiplier() =>
        swayMultCurve.Evaluate(GetDrunkRatio());

    public float GetMovementPenalty() =>
        movementPenalty.Evaluate(GetDrunkRatio());

    public float GetReloadTimeMult() =>
        reloadTimeMultCurve.Evaluate(GetDrunkRatio());
}