using UnityEngine;

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

        main.startColor =
            new Color(0.08f, 0.08f, 0.08f, 0.85f);

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;

        main.maxParticles = 100;

        var emission = _ps.emission;

        emission.enabled = false;

        var shape = _ps.shape;

        shape.enabled = true;

        shape.shapeType =
            ParticleSystemShapeType.Cone;

        shape.angle = coneAngle;

        shape.radius = coneRadius;

        var sizeOverLifetime =
            _ps.sizeOverLifetime;

        sizeOverLifetime.enabled = true;

        AnimationCurve sizeCurve =
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(0.5f, 0.9f),
                new Keyframe(1f, 1.5f)
            );

        sizeOverLifetime.size =
            new ParticleSystem.MinMaxCurve(
                1f,
                sizeCurve
            );

        var colorOverLifetime =
            _ps.colorOverLifetime;

        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(

            new GradientColorKey[]
            {
                new GradientColorKey(
                    new Color(0.08f, 0.08f, 0.08f),
                    0f
                ),

                new GradientColorKey(
                    new Color(0.15f, 0.15f, 0.15f),
                    0.5f
                ),

                new GradientColorKey(
                    new Color(0.25f, 0.25f, 0.25f),
                    1f
                )
            },

            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),

                new GradientAlphaKey(0.35f, 0.5f),

                new GradientAlphaKey(0f, 1f)
            }
        );

        colorOverLifetime.color = gradient;

        var noise = _ps.noise;

        noise.enabled = true;

        noise.strength = 0.4f;

        noise.frequency = 0.5f;

        noise.scrollSpeed = 0.2f;

        var velocityOverLifetime =
            _ps.velocityOverLifetime;

        velocityOverLifetime.enabled = true;

        velocityOverLifetime.y =
            new ParticleSystem.MinMaxCurve(0.2f);

        var renderer =
            _ps.GetComponent<ParticleSystemRenderer>();

        renderer.renderMode =
            ParticleSystemRenderMode.Billboard;

        renderer.material =
            new Material(
                Shader.Find("Particles/Standard Unlit")
            );
    }

    public void Emit()
    {
        if (_ps == null) return;

        Debug.Log("Smoke emitted");

        _ps.Emit(burstCount);
    }
}