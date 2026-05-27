using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSMultiplayer.Gameplay;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target Player")]
    [SerializeField] private PlayerController player;

    [Header("Crosshair")]
    [SerializeField] private RectTransform crosshairDot;

    [SerializeField] private float crosshairBaseSize = 8f;

    [SerializeField] private float crosshairMaxSize = 28f;

    [Header("Ammo")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [SerializeField] private TextMeshProUGUI reloadText;

    [Header("Health Bar")]
    [SerializeField] private Slider healthSlider;

    [SerializeField] private Image healthFill;

    [Header("Stamina Bar")]
    [SerializeField] private Slider staminaSlider;

    [SerializeField] private Image staminaFill;

    [Header("Drunk Bar")]
    [SerializeField] private Slider drunkSlider;

    [SerializeField] private Image drunkFill;

    [Header("Death Panel")]
    [SerializeField] private GameObject deathPanel;

    private WeaponSystem _weapon;

    private HealthSystem _health;

    private DrunkSystem _drunk;
    private bool _bound;

    private static readonly Color _healthHigh =
        new Color(0.22f, 0.78f, 0.45f);

    private static readonly Color _healthMid =
        new Color(0.95f, 0.75f, 0.10f);

    private static readonly Color _healthLow =
        new Color(0.87f, 0.25f, 0.20f);

    private static readonly Color _staminaColor =
        new Color(0.25f, 0.65f, 0.95f);

    private static readonly Color _drunkLow =
        new Color(0.95f, 0.85f, 0.20f);

    private static readonly Color _drunkHigh =
        new Color(0.90f, 0.35f, 0.10f);

    private void Awake()
    {
        if (player == null)
            player = FindLocalPlayer();

        BindPlayer();

        if (reloadText != null)
            reloadText.gameObject.SetActive(false);

        if (deathPanel != null)
            deathPanel.SetActive(false);

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;

            staminaSlider.maxValue = 1f;

            staminaSlider.value = 1f;
        }

        if (staminaFill != null)
            staminaFill.color = _staminaColor;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;

            healthSlider.maxValue = 1f;

            healthSlider.value = 1f;
        }
    }

    private PlayerController FindLocalPlayer()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.HasInputAuthority)
                return p;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnHealthChanged.RemoveListener(OnHealthChanged);

            _health.OnDeath.RemoveListener(OnDeath);
            _health.OnRespawn.RemoveListener(OnRespawn);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindLocalPlayer();
            BindPlayer();
        }

        if (player == null || !player.HasInputAuthority)
            return;

        UpdateCrosshair();

        UpdateAmmo();

        UpdateStamina();

        UpdateDrunk();
    }

    private void OnHealthChanged(float current, float max)
    {
        float ratio =
            max > 0f
            ? current / max
            : 0f;

        SetHealthBar(ratio);
    }

    private void SetHealthBar(float ratio)
    {
        if (healthSlider != null)
            healthSlider.value = ratio;

        if (healthFill != null)
        {
            healthFill.color =
                ratio > 0.6f
                ? _healthHigh
                : ratio > 0.3f
                    ? Color.Lerp(
                        _healthMid,
                        _healthHigh,
                        (ratio - 0.3f) / 0.3f
                    )
                    : Color.Lerp(
                        _healthLow,
                        _healthMid,
                        ratio / 0.3f
                    );
        }
    }

    private void OnDeath()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);

        SetHealthBar(0f);
    }

    private void OnRespawn()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);

        if (_health != null)
            SetHealthBar(_health.GetHealthRatio());
    }

    private void UpdateCrosshair()
    {
        if (crosshairDot == null)
            return;

        float ratio =
            _drunk != null
            ? _drunk.GetDrunkRatio()
            : 0f;

        float size =
            Mathf.Lerp(
                crosshairBaseSize,
                crosshairMaxSize,
                ratio
            );

        crosshairDot.sizeDelta =
            new Vector2(size, size);
    }

    private void UpdateAmmo()
    {
        if (_weapon == null)
            return;

        if (ammoText != null)
        {
            ammoText.text =
                _weapon.IsReloading
                ? "- / -"
                : $"{_weapon.CurrentAmmo} / {_weapon.CylinderSize}";
        }

        if (reloadText != null)
            reloadText.gameObject.SetActive(_weapon.IsReloading);
    }

    private void UpdateStamina()
    {
        if (player == null || staminaSlider == null)
            return;

        float ratio =
            player.CurrentStamina /
            player.MaxStamina;

        staminaSlider.value = ratio;
    }

    private void UpdateDrunk()
    {
        if (_drunk == null || drunkSlider == null)
            return;

        float ratio =
            _drunk.GetDrunkRatio();

        drunkSlider.value = ratio;

        if (drunkFill != null)
        {
            drunkFill.color =
                Color.Lerp(
                    _drunkLow,
                    _drunkHigh,
                    ratio
                );
        }
    }

    private void BindPlayer()
    {
        if (player == null || _bound)
            return;

        _weapon = player.GetComponentInChildren<WeaponSystem>();
        _health = player.GetComponent<HealthSystem>();
        _drunk = player.GetComponent<DrunkSystem>();

        if (_health != null)
        {
            _health.OnHealthChanged.AddListener(OnHealthChanged);
            _health.OnDeath.AddListener(OnDeath);
            _health.OnRespawn.AddListener(OnRespawn);
            SetHealthBar(_health.GetHealthRatio());
        }

        _bound = true;
    }
}