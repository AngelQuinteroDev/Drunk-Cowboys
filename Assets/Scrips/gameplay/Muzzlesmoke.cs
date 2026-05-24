using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class MuzzleSmoke : MonoBehaviour
{
    [Header("Smoke Settings")]
    [SerializeField] private int burstCount = 10;
    [SerializeField] private float startSpeed = 1.5f;
    [SerializeField] private float startSize = 0.5f;
    [SerializeField] private float lifetime = 1.2f;
    [SerializeField] private float coneAngle = 15f;
    [SerializeField] private float coneRadius = 0.02f;

    [Header("Material")]
    [Tooltip("Arrastra aqui un material de particulas de tu proyecto. Si lo dejas vacio el script intentara asignar uno automaticamente.")]
    [SerializeField] private Material smokeMaterial;

    private ParticleSystem _ps;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        SetupParticleSystem();
    }

    private void SetupParticleSystem()
    {
        var main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = lifetime;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        var emission = _ps.emission;
        emission.enabled = false;

        var shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = coneRadius;

        var sol = _ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.5f, 0.9f),
                new Keyframe(1f, 1.5f)));

        var col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 0f),
                new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 0.5f),
                new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f,  0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0f,    1f)
            });
        col.color = grad;

        var noise = _ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;

        var vol = _ps.velocityOverLifetime;
        vol.enabled = true;
        vol.y = new ParticleSystem.MinMaxCurve(0.2f);

        AssignMaterial();
    }

    private void AssignMaterial()
    {
        var rend = _ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 1;

        if (smokeMaterial != null)
        {
            rend.material = smokeMaterial;
            return;
        }

        string shaderName = GetParticleShaderName();
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogWarning("[MuzzleSmoke] No se encontro el shader '" + shaderName +
                "'. Arrastra un Material de particulas al campo Smoke Material en el Inspector.");
            return;
        }

        Material mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.renderQueue = 3000;
        rend.material = mat;
    }

    private string GetParticleShaderName()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
            return "Particles/Standard Unlit";

        string name = pipeline.GetType().Name;
        if (name.Contains("Universal") || name.Contains("URP"))
            return "Universal Render Pipeline/Particles/Unlit";
        if (name.Contains("HighDefinition") || name.Contains("HDRP"))
            return "HDRP/Particles/Unlit";

        return "Particles/Standard Unlit";
    }

    public void Emit()
    {
        if (_ps == null) return;
        _ps.Emit(burstCount);
    }
}