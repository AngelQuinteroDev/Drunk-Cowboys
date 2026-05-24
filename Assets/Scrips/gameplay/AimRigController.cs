using UnityEngine;
using UnityEngine.Animations.Rigging;

public class AimRigController : MonoBehaviour
{
    [Header("Animator Object (Cowboy@Drunk Idle)")]
    [Tooltip("El objeto que tiene el componente Animator. NO es el Player raiz.")]
    [SerializeField] private Animator animatorObject;

    [Header("Aim Target")]
    [SerializeField] private Transform aimTarget;

    [Header("Mixamo Bones")]
    [SerializeField] private Transform boneHead;
    [SerializeField] private Transform boneNeck;
    [SerializeField] private Transform boneSpineUpper;
    [SerializeField] private Transform boneSpineLower;

    [Header("Aim Weights")]
    [SerializeField][Range(0f, 1f)] private float weightHead = 1.0f;
    [SerializeField][Range(0f, 1f)] private float weightNeck = 0.7f;
    [SerializeField][Range(0f, 1f)] private float weightSpineUpper = 0.4f;
    [SerializeField][Range(0f, 1f)] private float weightSpineLower = 0.15f;

    [Header("Axes")]
    [SerializeField] private Vector3 aimAxis = Vector3.forward;
    [SerializeField] private Vector3 upAxis = Vector3.up;

    private MultiAimConstraint _headConstraint;
    private MultiAimConstraint _neckConstraint;
    private MultiAimConstraint _spineUpperConstraint;
    private MultiAimConstraint _spineLowerConstraint;

    private RigBuilder _rigBuilder;
    private GameObject _rigLayerGO;

    private void Start()
    {
        if (animatorObject == null)
            animatorObject = GetComponentInChildren<Animator>();

        if (animatorObject == null)
        {
            Debug.LogError("[AimRigController] No Animator found. Assign 'Cowboy@Drunk Idle' to Animator Object.");
            return;
        }

        if (aimTarget == null)
        {
            Debug.LogError("[AimRigController] Aim Target not assigned.");
            return;
        }

        BuildRig();
    }

    private void BuildRig()
    {
        _rigBuilder = animatorObject.GetComponent<RigBuilder>();
        if (_rigBuilder == null)
            _rigBuilder = animatorObject.gameObject.AddComponent<RigBuilder>();

        _rigLayerGO = new GameObject("AimRigLayer");
        _rigLayerGO.transform.SetParent(animatorObject.transform);
        _rigLayerGO.transform.localPosition = Vector3.zero;
        _rigLayerGO.transform.localRotation = Quaternion.identity;
        _rigLayerGO.transform.localScale = Vector3.one;

        Rig rig = _rigLayerGO.AddComponent<Rig>();

        if (boneHead != null)
            _headConstraint = CreateConstraint("HeadAim", boneHead, weightHead);

        if (boneNeck != null)
            _neckConstraint = CreateConstraint("NeckAim", boneNeck, weightNeck);

        if (boneSpineUpper != null)
            _spineUpperConstraint = CreateConstraint("SpineUpperAim", boneSpineUpper, weightSpineUpper);

        if (boneSpineLower != null)
            _spineLowerConstraint = CreateConstraint("SpineLowerAim", boneSpineLower, weightSpineLower);

        _rigBuilder.layers.Clear();
        _rigBuilder.layers.Add(new RigLayer(rig, true));
        _rigBuilder.Build();
    }

    private MultiAimConstraint CreateConstraint(string constraintName, Transform bone, float weight)
    {
        GameObject go = new GameObject(constraintName);
        go.transform.SetParent(_rigLayerGO.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        MultiAimConstraint c = go.AddComponent<MultiAimConstraint>();
        c.data.constrainedObject = bone;
        c.data.aimAxis = ConvertAxis(aimAxis);
        c.data.upAxis = ConvertAxis(upAxis);

        WeightedTransformArray sources = new WeightedTransformArray();
        sources.Add(new WeightedTransform(aimTarget, 1f));
        c.data.sourceObjects = sources;

        c.weight = weight;
        return c;
    }

    private MultiAimConstraintData.Axis ConvertAxis(Vector3 v)
    {
        if (v == Vector3.right) return MultiAimConstraintData.Axis.X;
        if (v == Vector3.left) return MultiAimConstraintData.Axis.X_NEG;
        if (v == Vector3.up) return MultiAimConstraintData.Axis.Y;
        if (v == Vector3.down) return MultiAimConstraintData.Axis.Y_NEG;
        if (v == Vector3.back) return MultiAimConstraintData.Axis.Z_NEG;
        return MultiAimConstraintData.Axis.Z;
    }

    public void SetAimWeight(float head, float neck, float spineUpper, float spineLower)
    {
        if (_headConstraint != null) _headConstraint.weight = head;
        if (_neckConstraint != null) _neckConstraint.weight = neck;
        if (_spineUpperConstraint != null) _spineUpperConstraint.weight = spineUpper;
        if (_spineLowerConstraint != null) _spineLowerConstraint.weight = spineLower;
    }

    public void DisableAim() => SetAimWeight(0f, 0f, 0f, 0f);
    public void EnableAim() => SetAimWeight(weightHead, weightNeck, weightSpineUpper, weightSpineLower);
}