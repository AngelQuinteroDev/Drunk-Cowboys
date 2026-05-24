using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
public class Turret : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float fieldOfView = 120f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Rotation")]
    [SerializeField] private Transform rotatingHead;
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private float verticalMin = -20f;
    [SerializeField] private float verticalMax = 45f;

    [Header("Shooting")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private float bulletSpeed = 25f;
    [SerializeField] private float damagePerShot = 10f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color idleColor = Color.green;
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;

    private enum TurretState { Idle, Alert, Attacking }

    private TurretState _state = TurretState.Idle;
    private Transform _target;
    private float _fireTimer;
    private HealthSystem _health;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _mpb = new MaterialPropertyBlock();

        if (_health != null)
            _health.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_health != null && !_health.IsAlive) return;

        DetectPlayer();
        UpdateState();
        UpdateColor();
    }

    private void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);

        _target = null;

        foreach (Collider hit in hits)
        {
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToTarget);

            if (angle > fieldOfView * 0.5f) continue;

            if (!Physics.Linecast(transform.position + Vector3.up, hit.transform.position + Vector3.up * 1.5f, out RaycastHit lineHit))
            {
                _target = hit.transform;
                break;
            }

            if (lineHit.collider.GetComponentInParent<HealthSystem>() != null &&
                lineHit.collider.GetComponentInParent<HealthSystem>() == hit.GetComponentInParent<HealthSystem>())
            {
                _target = hit.transform;
                break;
            }
        }
    }

    private void UpdateState()
    {
        if (_target == null)
        {
            _state = TurretState.Idle;
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist > detectionRange)
        {
            _state = TurretState.Idle;
            return;
        }

        _state = TurretState.Attacking;

        RotateTowardTarget();

        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f)
        {
            _fireTimer = fireInterval;
            Shoot();
        }
    }

    private void RotateTowardTarget()
    {
        if (rotatingHead == null || _target == null) return;

        Vector3 dir = (_target.position + Vector3.up * 1.5f) - rotatingHead.position;
        Quaternion targetRot = Quaternion.LookRotation(dir);

        Vector3 euler = targetRot.eulerAngles;
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        pitch = Mathf.Clamp(pitch, verticalMin, verticalMax);
        targetRot = Quaternion.Euler(pitch, euler.y, 0f);

        rotatingHead.rotation = Quaternion.RotateTowards(
            rotatingHead.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    private void Shoot()
    {
        if (bulletPrefab == null || muzzle == null) return;

        Vector3 dir = _target != null
            ? (_target.position + Vector3.up * 1.5f - muzzle.position).normalized
            : muzzle.forward;

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(dir));

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = dir * bulletSpeed;
        }

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null) b.SetDamage(damagePerShot);

        Destroy(bullet, 4f);
    }

    private void UpdateColor()
    {
        if (bodyRenderer == null) return;

        Color target = _state switch
        {
            TurretState.Alert => alertColor,
            TurretState.Attacking => attackColor,
            _ => idleColor
        };

        _mpb.SetColor("_BaseColor", target);
        _mpb.SetColor("_Color", target);
        bodyRenderer.SetPropertyBlock(_mpb);
    }

    private void OnDeath()
    {
        if (rotatingHead != null)
            rotatingHead.rotation = Quaternion.Euler(90f, rotatingHead.eulerAngles.y, 0f);

        if (bodyRenderer != null)
        {
            _mpb.SetColor("_BaseColor", Color.black);
            _mpb.SetColor("_Color", Color.black);
            bodyRenderer.SetPropertyBlock(_mpb);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position, left * detectionRange);
        Gizmos.DrawRay(transform.position, right * detectionRange);
    }
}