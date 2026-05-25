using UnityEngine;

public class DrunkAimModifier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AimRigController aimRig;
    [SerializeField] private DrunkSystem      drunkSystem;

    [Header("Weights when sober (full aim)")]
    [SerializeField] private float soberHead       = 1.0f;
    [SerializeField] private float soberNeck       = 0.7f;
    [SerializeField] private float soberSpineUpper = 0.4f;
    [SerializeField] private float soberSpineLower = 0.15f;

    [Header("Weights when fully drunk (broken aim)")]
    [SerializeField] private float drunkHead       = 0.3f;
    [SerializeField] private float drunkNeck       = 0.2f;
    [SerializeField] private float drunkSpineUpper = 0.1f;
    [SerializeField] private float drunkSpineLower = 0.05f;

    private void Awake()
    {
        if (drunkSystem == null)
            drunkSystem = GetComponentInParent<DrunkSystem>();

        if (aimRig == null)
            aimRig = GetComponentInParent<AimRigController>();
    }

    private void Update()
    {
        if (aimRig == null || drunkSystem == null) return;

        float t = drunkSystem.GetDrunkRatio();

        aimRig.SetAimWeight(
            Mathf.Lerp(soberHead,       drunkHead,       t),
            Mathf.Lerp(soberNeck,       drunkNeck,       t),
            Mathf.Lerp(soberSpineUpper, drunkSpineUpper, t),
            Mathf.Lerp(soberSpineLower, drunkSpineLower, t)
        );
    }
}
