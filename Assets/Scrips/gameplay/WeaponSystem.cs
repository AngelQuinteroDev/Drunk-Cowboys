using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    [Header("Revolver Stats")]
    [SerializeField] private int cylinderSize = 5;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseReloadTime = 2f;

    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;

    [Header("Spread")]
    [SerializeField] private float baseSpread = 1.5f;

    [Header("Damage Bonus From Drunk")]
    [SerializeField] private float maxDamageMult = 2f;

    [Header("Recoil")]
    [SerializeField] private float recoilKick = 3f;

    [Header("Weapon Sway")]
    [SerializeField] private float baseSway = 0.015f;
    [SerializeField] private float swaySpeed = 1.5f;

    [Header("References")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private Transform aimTarget;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public int CylinderSize => cylinderSize;

    private float _nextFireTime;
    private Vector3 _defaultWeaponPos;
    private DrunkSystem _drunk;
    private ParticleSystem _smokePS;

    private void Awake()
    {
        _drunk = GetComponentInParent<DrunkSystem>();
        CurrentAmmo = cylinderSize;

        if (weaponVisual != null)
            _defaultWeaponPos = weaponVisual.localPosition;

        SetupMuzzleSmoke();
    }

    private void SetupMuzzleSmoke()
    {
        if (muzzlePoint == null) return;

        _smokePS = muzzlePoint.GetComponent<ParticleSystem>();

        if (_smokePS == null)
            _smokePS = muzzlePoint.gameObject.AddComponent<ParticleSystem>();

        var main = _smokePS.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 2f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var emission = _smokePS.emission;
        emission.enabled = false;

        var shape = _smokePS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.01f;

        var sol = _smokePS.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 0.3f),
                new Keyframe(1f, 1.2f)
            ));

        var col = _smokePS.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white,                  0f),
                new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = grad;
    }

    private void Update()
    {
        HandleSway();
    }

    public void TryShoot()
    {
        if (IsReloading || Time.time < _nextFireTime) return;

        if (CurrentAmmo <= 0)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        Shoot();
    }

    public void TryReload()
    {
        if (!IsReloading && CurrentAmmo < cylinderSize)
            StartCoroutine(ReloadRoutine());
    }

    private void Shoot()
    {
        CurrentAmmo--;
        _nextFireTime = Time.time + 1f / fireRate;

        Vector3 origin = muzzlePoint != null
            ? muzzlePoint.position
            : transform.position;

        Vector3 direction = GetShootDirection(origin);
        direction = ApplySpread(direction);

        SpawnBullet(origin, direction);
        ApplyRecoil();
        EmitSmoke();

        if (CurrentAmmo <= 0)
            StartCoroutine(ReloadRoutine());
    }

    private Vector3 GetShootDirection(Vector3 origin)
    {
        if (aimTarget != null)
            return (aimTarget.position - origin).normalized;

        if (Camera.main != null)
            return Camera.main.transform.forward;

        return transform.forward;
    }

    private void SpawnBullet(Vector3 origin, Vector3 direction)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
            bulletScript.SetDamage(CalculateDamage());

        Collider bulletCol = bullet.GetComponent<Collider>();
        Collider playerCol = GetComponentInParent<Collider>();
        if (bulletCol != null && playerCol != null)
            Physics.IgnoreCollision(bulletCol, playerCol);
    }

    private void EmitSmoke()
    {
        if (_smokePS != null)
            _smokePS.Emit(8);
    }

    private float CalculateDamage()
    {
        if (_drunk == null) return baseDamage;
        return baseDamage * Mathf.Lerp(1f, maxDamageMult, _drunk.GetDrunkRatio());
    }

    private Vector3 ApplySpread(Vector3 direction)
    {
        float penalty = _drunk != null ? _drunk.GetSpreadPenalty() : 0f;
        float spread = baseSpread + penalty;
        direction += Random.insideUnitSphere * Mathf.Tan(spread * Mathf.Deg2Rad);
        return direction.normalized;
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        float reloadTime = _drunk != null
            ? baseReloadTime * _drunk.GetReloadTimeMult()
            : baseReloadTime;
        yield return new WaitForSeconds(reloadTime);
        CurrentAmmo = cylinderSize;
        IsReloading = false;
    }

    private void ApplyRecoil()
    {
        if (weaponVisual == null) return;
        weaponVisual.localPosition -= Vector3.forward * recoilKick * 0.01f;
    }

    private void HandleSway()
    {
        if (weaponVisual == null) return;

        float mult = _drunk != null ? _drunk.GetSwayMultiplier() : 1f;
        float amplitude = baseSway * mult;
        float t = Time.time * swaySpeed;

        Vector3 offset = new Vector3(
            Mathf.Sin(t) * amplitude,
            Mathf.Sin(t * 0.7f) * amplitude * 0.5f,
            0f
        );

        weaponVisual.localPosition = Vector3.Lerp(
            weaponVisual.localPosition,
            _defaultWeaponPos + offset,
            Time.deltaTime * 8f
        );
    }
}