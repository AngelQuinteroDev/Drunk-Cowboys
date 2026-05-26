using Fusion;
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    public class Bullet : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 30f;
        [SerializeField] private float lifetime = 3f;

        [Header("Damage")]
        [SerializeField] private float damage = 20f;

        [Header("Effects")]
        [SerializeField] private GameObject impactVFXPrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip flySound;

        private Rigidbody _rb;
        private bool _applyDamage = false;

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
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.linearVelocity = transform.forward * speed;
            }

            if (audioSource != null && flySound != null)
            {
                audioSource.clip = flySound;
                audioSource.loop = true;
                audioSource.spatialBlend = 1f;
                audioSource.Play();
            }

            Destroy(gameObject, lifetime);
        }

        public void Initialize(float damage, bool applyDamage)
        {
            this.damage = damage;
            _applyDamage = applyDamage;
        }

        public void SetDamage(float value)
        {
            damage = value;
            _applyDamage = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_applyDamage)
            {
                var target = collision.collider.GetComponentInParent<HealthSystem>();

                if (target != null)
                {
                    if (target.Object != null && target.Object.IsValid)
                        target.RPC_ApplyDamage(damage, PlayerRef.None);
                    else
                        target.TakeDamage(damage);
                }
            }

            if (impactVFXPrefab != null)
            {
                ContactPoint contact = collision.GetContact(0);

                GameObject vfx = Instantiate(
                    impactVFXPrefab,
                    contact.point,
                    Quaternion.LookRotation(contact.normal)
                );

                Destroy(vfx, 2f);
            }

            Destroy(gameObject);
        }
    }
}