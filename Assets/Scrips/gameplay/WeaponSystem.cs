using UnityEngine;
using System.Collections;

public class WeaponSystem : MonoBehaviour
{
    [Header("Revolver Stats")]
    [SerializeField] private int cylinderSize = 5;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseReloadTime = 2f;

    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;

    [Header("Shot Delay (sync with animation)")]
    [Tooltip("Segundos de espera entre que el jugador dispara y sale la bala. Ajusta para que coincida con el frame del revolver en la animacion.")]
    [SerializeField] private float shotDelay = 0.08f;

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
    [SerializeField] private MuzzleSmoke muzzleSmoke;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public int CylinderSize => cylinderSize;

    private float _nextFireTime;
    private Vector3 _defaultWeaponPos;
    private DrunkSystem _drunk;

    private void Awake()
    {
        _drunk = GetComponentInParent<DrunkSystem>();
        CurrentAmmo = cylinderSize;

        if (weaponVisual != null)
            _defaultWeaponPos = weaponVisual.localPosition;

        if (muzzleSmoke == null)
            muzzleSmoke = GetComponentInChildren<MuzzleSmoke>();
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

        StartCoroutine(ShootWithDelay());
    }

    public void TryReload()
    {
        if (!IsReloading && CurrentAmmo < cylinderSize)
            StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ShootWithDelay()
    {
        CurrentAmmo--;
        _nextFireTime = Time.time + 1f / fireRate;
        ApplyRecoil();

        yield return new WaitForSeconds(shotDelay);

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector3 direction = GetShootDirection(origin);
        direction = ApplySpread(direction);

        SpawnBullet(origin, direction);

        if (muzzleSmoke != null)
            muzzleSmoke.Emit();

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

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null) b.SetDamage(CalculateDamage());

        Collider bulletCol = bullet.GetComponent<Collider>();
        Collider playerCol = GetComponentInParent<Collider>();
        if (bulletCol != null && playerCol != null)
            Physics.IgnoreCollision(bulletCol, playerCol);
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

    private IEnumerator ReloadRoutine()
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