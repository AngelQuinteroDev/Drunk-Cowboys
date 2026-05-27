using Fusion;
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    [RequireComponent(typeof(HealthSystem))]
    public class Turret : NetworkBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float     detectionRange = 20f;
        [SerializeField] private float     fieldOfView    = 120f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Rotation")]
        [SerializeField] private Transform rotatingHead;
        [SerializeField] private float     rotationSpeed = 60f;
        [SerializeField] private float     verticalMin   = -20f;
        [SerializeField] private float     verticalMax   = 45f;

        [Header("Shooting — Hitscan server-side")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject bulletVFXPrefab;   
        [SerializeField] private float     fireInterval   = 1.5f;
        [SerializeField] private float     damagePerShot  = 10f;
        [SerializeField] private float     maxRange       = 80f;
        [SerializeField] private LayerMask hitMask        = ~0;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color    idleColor   = Color.green;
        [SerializeField] private Color    alertColor  = Color.yellow;
        [SerializeField] private Color    attackColor = Color.red;

        [Networked] private TurretStateNet NetState   { get; set; }
        [Networked] public  bool           IsDestroyed { get; private set; }

        private enum TurretStateNet : byte { Idle, Attacking }

        private Transform      _target;
        private float          _fireTimer;
        private HealthSystem   _health;
        private MaterialPropertyBlock _mpb;
        private ChangeDetector _changes;

        public override void Spawned()
        {
            _health  = GetComponent<HealthSystem>();
            _mpb     = new MaterialPropertyBlock();
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (_health != null)
                _health.OnDeath.AddListener(OnDeath);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)  return;
            if (IsDestroyed)         return;
            if (_health != null && !_health.IsAlive) return;

            DetectPlayer();
            UpdateStateMachine();
        }

        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                if (change == nameof(NetState))
                    UpdateColor();
                if (change == nameof(IsDestroyed) && IsDestroyed)
                    OnDeathVisual();
            }
        }

        private void DetectPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, detectionRange, playerLayer);

            _target = null;

            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<HealthSystem>();
                if (health == null || !health.IsAlive) continue;

                Vector3 dir   = (hit.transform.position - transform.position).normalized;
                float   angle = Vector3.Angle(transform.forward, dir);
                if (angle > fieldOfView * 0.5f) continue;

                Vector3 eyePos    = transform.position + Vector3.up;
                Vector3 targetPos = hit.transform.position + Vector3.up * 1.5f;

                if (Physics.Linecast(eyePos, targetPos, out RaycastHit lineHit))
                {
                   
                    if (lineHit.collider.GetComponentInParent<HealthSystem>() != health)
                        continue;
                }

                _target = hit.transform;
                break;
            }
        }

        private void UpdateStateMachine()
        {
            if (_target == null)
            {
                NetState = TurretStateNet.Idle;
                return;
            }

            NetState = TurretStateNet.Attacking;
            RotateTowardTarget();

            _fireTimer -= Runner.DeltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = fireInterval;
                FireHitscan();
            }
        }

        private void RotateTowardTarget()
        {
            if (rotatingHead == null || _target == null) return;

            Vector3    dir       = (_target.position + Vector3.up * 1.5f) - rotatingHead.position;
            Quaternion targetRot = Quaternion.LookRotation(dir);

            Vector3 euler = targetRot.eulerAngles;
            float   pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            pitch = Mathf.Clamp(pitch, verticalMin, verticalMax);
            targetRot = Quaternion.Euler(pitch, euler.y, 0f);

            rotatingHead.rotation = Quaternion.RotateTowards(
                rotatingHead.rotation,
                targetRot,
                rotationSpeed * Runner.DeltaTime
            );
        }

        private void FireHitscan()
        {
            if (muzzle == null || _target == null) return;

            Vector3 origin = muzzle.position;
            Vector3 dir    = (_target.position + Vector3.up * 1.5f - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, hitMask,
                QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<HealthSystem>();
                target?.TakeDamage(damagePerShot, PlayerRef.None);

                RPC_PlayFireVFX(origin, hit.point);
            }
            else
            {
                RPC_PlayFireVFX(origin, origin + dir * maxRange);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayFireVFX(Vector3 origin, Vector3 hitPoint)
        {
            if (bulletVFXPrefab == null) return;

            Vector3    dir    = (hitPoint - origin).normalized;
            GameObject visual = Instantiate(
                bulletVFXPrefab, origin, Quaternion.LookRotation(dir));
            Destroy(visual, 3f);
        }

        private void UpdateColor()
        {
            if (bodyRenderer == null) return;

            Color target = NetState == TurretStateNet.Attacking ? attackColor : idleColor;
            _mpb.SetColor("_BaseColor", target);
            _mpb.SetColor("_Color",     target);
            bodyRenderer.SetPropertyBlock(_mpb);
        }

        private void OnDeath()
        {
            if (HasStateAuthority)
                IsDestroyed = true;
        }

        private void OnDeathVisual()
        {
            if (rotatingHead != null)
                rotatingHead.rotation = Quaternion.Euler(
                    90f, rotatingHead.eulerAngles.y, 0f);

            if (bodyRenderer != null)
            {
                _mpb.SetColor("_BaseColor", Color.black);
                _mpb.SetColor("_Color",     Color.black);
                bodyRenderer.SetPropertyBlock(_mpb);
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Vector3 left  = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f,  fieldOfView * 0.5f, 0f) * transform.forward;
            Gizmos.DrawRay(transform.position, left  * detectionRange);
            Gizmos.DrawRay(transform.position, right * detectionRange);
        }
    }
}