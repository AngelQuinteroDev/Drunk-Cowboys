using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifetime = 3f;

    [Header("Damage")]
    [SerializeField] private float damage = 20f;

    [Header("Effects")]
    [SerializeField] private GameObject impactVFXPrefab;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (_rb != null)
        {
            _rb.useGravity = false;

            _rb.linearDamping = 0f;

            _rb.angularDamping = 0f;

            _rb.interpolation =
                RigidbodyInterpolation.Interpolate;

            _rb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            _rb.linearVelocity =
                transform.forward * speed;
        }

        Destroy(gameObject, lifetime);
    }

    public void SetDamage(float value)
    {
        damage = value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        HealthSystem target =
            collision.collider.GetComponentInParent<HealthSystem>();

        if (target != null)
            target.TakeDamage(damage);

        if (impactVFXPrefab != null)
        {
            ContactPoint contact =
                collision.GetContact(0);

            GameObject vfx =
                Instantiate(
                    impactVFXPrefab,
                    contact.point,
                    Quaternion.LookRotation(contact.normal)
                );

            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }
}