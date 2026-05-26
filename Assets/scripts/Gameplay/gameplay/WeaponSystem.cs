using Fusion;
using UnityEngine;
using FPSMultiplayer.Gameplay;

public class WeaponSystem : NetworkBehaviour
{
    [Header("Revolver Stats")]
    [SerializeField] private int cylinderSize = 5;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseReloadTime = 2f;

    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;

    [Header("Shot Delay")]
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

    [Header("Hit Scan")]
    [SerializeField] private float maxRange = 120f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("References")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private MuzzleSmoke muzzleSmoke;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;

    [Networked] public int CurrentAmmo { get; private set; }
    [Networked] public bool IsReloading { get; private set; }
    [Networked] public bool IsFiring { get; private set; }
    [Networked] private TickTimer FireCooldown { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private TickTimer FireFxTimer { get; set; }

    public int CylinderSize => cylinderSize;
    public bool CanShoot => !IsReloading && CurrentAmmo > 0 && FireCooldown.ExpiredOrNotRunning(Runner);

    private Vector3 _defaultWeaponPos;
    private DrunkSystem _drunk;
    private TickTimer _shotDelayTimer;
    private bool _shotQueued;
    private Vector3 _queuedOrigin;
    private Vector3 _queuedDirection;
    private PlayerRef _queuedOwner;

    public override void Spawned()
    {
        _drunk = GetComponentInParent<DrunkSystem>();

        if (HasStateAuthority)
            CurrentAmmo = cylinderSize;

        if (weaponVisual != null)
            _defaultWeaponPos = weaponVisual.localPosition;

        if (muzzleSmoke == null)
            muzzleSmoke = GetComponentInChildren<MuzzleSmoke>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (IsReloading && ReloadTimer.Expired(Runner))
            FinishReload();

        if (IsFiring && FireFxTimer.Expired(Runner))
            IsFiring = false;

        if (_shotQueued && _shotDelayTimer.Expired(Runner))
        {
            _shotQueued = false;
            FireShot(_queuedOrigin, _queuedDirection, _queuedOwner);
        }
    }

    public override void Render()
    {
        if (HasInputAuthority)
            HandleSway();
    }

    /// <summary>
    /// Returns true if a shot was actually fired this tick.
    /// </summary>
    public bool ProcessInput(bool fire, bool reload, Vector3 origin, Vector3 direction, PlayerRef owner)
    {
        if (!HasStateAuthority) return false;

        if (reload)
            TryReload();

        if (fire)
            return TryShoot(origin, direction, owner);

        return false;
    }

    private bool TryShoot(Vector3 origin, Vector3 direction, PlayerRef owner)
    {
        if (IsReloading) return false;
        if (!FireCooldown.ExpiredOrNotRunning(Runner)) return false;

        if (CurrentAmmo <= 0)
        {
            TryReload();
            return false;
        }

        CurrentAmmo--;
        FireCooldown = TickTimer.CreateFromSeconds(Runner, 1f / fireRate);
        IsFiring = true;
        FireFxTimer = TickTimer.CreateFromSeconds(Runner, 0.12f);

        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound);

        Vector3 shotOrigin = muzzlePoint != null ? muzzlePoint.position : origin;
        Vector3 shotDir = ApplySpread(direction.normalized);

        if (shotDelay > 0f)
        {
            _shotQueued = true;
            _queuedOrigin = shotOrigin;
            _queuedDirection = shotDir;
            _queuedOwner = owner;
            _shotDelayTimer = TickTimer.CreateFromSeconds(Runner, shotDelay);
        }
        else
        {
            FireShot(shotOrigin, shotDir, owner);
        }

        if (CurrentAmmo <= 0)
            TryReload();

        return true;
    }

    private void FireShot(Vector3 origin, Vector3 direction, PlayerRef owner)
    {
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<HealthSystem>();
            if (target != null)
                target.TakeDamage(CalculateDamage(), owner);
        }

        RPC_PlayFireFx(origin, direction);
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

    private async void TryReload()
    {
        if (IsReloading || CurrentAmmo >= cylinderSize) return;

        IsReloading = true;

        await System.Threading.Tasks.Task.Delay(180);

        if (audioSource != null && reloadSound != null)
            audioSource.PlayOneShot(reloadSound);

        float reloadTime = _drunk != null
            ? baseReloadTime * _drunk.GetReloadTimeMult()
            : baseReloadTime;

        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
    }

    private void FinishReload()
    {
        CurrentAmmo = cylinderSize;
        IsReloading = false;
        ReloadTimer = default;
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

    public void ResetAmmo()
    {
        if (!HasStateAuthority) return;
        CurrentAmmo = cylinderSize;
        IsReloading = false;
        ReloadTimer = default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFireFx(Vector3 origin, Vector3 direction)
    {
        if (muzzleSmoke != null)
            muzzleSmoke.Emit();

        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));

            if (bullet.TryGetComponent<Bullet>(out var b))
                b.Initialize(0f, false);

            Collider bulletCol = bullet.GetComponent<Collider>();
            Collider playerCol = GetComponentInParent<Collider>();
            if (bulletCol != null && playerCol != null)
                Physics.IgnoreCollision(bulletCol, playerCol);
        }

        if (HasInputAuthority)
            ApplyRecoil();
    }
}