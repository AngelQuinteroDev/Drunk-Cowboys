// ============================================================
//  Bullet — Fusion 2, Player Host Topology
//
//  ARQUITECTURA DE DISPARO EN ESTE PROYECTO:
//  El WeaponSystem usa HITSCAN (Physics.Raycast server-side).
//  La bala física es SOLO un efecto visual — no aplica daño.
//
//  Flujo correcto:
//    1. Host ejecuta raycast → detecta hit → aplica daño directo en HealthSystem
//    2. Host llama RPC_PlayFireFx() → todos los clientes instancian la bala visual
//    3. La bala visual vuela, colisiona, genera VFX de impacto y se destruye
//
//  Por eso Initialize(damage=0, applyDamage=false) en WeaponSystem.
//
//  RESPONSABILIDADES:
//    TODOS : movimiento visual, VFX de impacto, autodestroy
//    NADIE : daño (el raycast del host ya lo hizo)
// ============================================================
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    // MonoBehaviour es correcto aquí: la bala es un objeto visual local
    // instanciado vía Instantiate() en el RPC, no un NetworkObject.
    // Spawnearlo como NetworkObject solo añadiría overhead innecesario
    // para algo que no necesita estado replicado.
    public class Bullet : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed    = 30f;
        [SerializeField] private float lifetime = 3f;

        [Header("Damage")]
        [SerializeField] private float damage = 20f;

        [Header("Effects")]
        [SerializeField] private GameObject impactVFXPrefab;

        private Rigidbody _rb;

        // applyDamage se usa solo si se quiere dano por colision.
        private bool _applyDamage = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (_rb != null)
            {
                _rb.useGravity           = false;
                _rb.linearDamping        = 0f;
                _rb.angularDamping       = 0f;
                _rb.interpolation        = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.linearVelocity       = transform.forward * speed;
            }

            Destroy(gameObject, lifetime);
        }

        // Llamado desde WeaponSystem.RPC_PlayFireFx
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
                    target.TakeDamage(damage);
            }

            // VFX de impacto — corre en todos los clientes, puramente visual
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