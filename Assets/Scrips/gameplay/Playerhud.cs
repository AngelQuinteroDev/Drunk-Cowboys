using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaDrainRate = 20f;
    [SerializeField] private float staminaRegenRate = 12f;
    [SerializeField] private float staminaRegenDelay = 1.5f;

    [Header("Drunk Bar")]
    [SerializeField] private Slider drunkSlider;
    [SerializeField] private Image drunkFill;

    private WeaponSystem _weapon;
    private HealthSystem _health;
    private DrunkSystem _drunk;

    private float _currentStamina;
    private float _staminaRegenTimer;

    private static readonly Color _healthHigh = new Color(0.22f, 0.78f, 0.45f);
    private static readonly Color _healthMid = new Color(0.95f, 0.75f, 0.10f);
    private static readonly Color _healthLow = new Color(0.87f, 0.25f, 0.20f);
    private static readonly Color _staminaColor = new Color(0.25f, 0.65f, 0.95f);
    private static readonly Color _drunkLow = new Color(0.95f, 0.85f, 0.20f);
    private static readonly Color _drunkHigh = new Color(0.90f, 0.35f, 0.10f);

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            _weapon = player.GetComponentInChildren<WeaponSystem>();
            _health = player.GetComponent<HealthSystem>();
            _drunk = player.GetComponent<DrunkSystem>();
        }

        _currentStamina = staminaMax;

        if (reloadText != null)
            reloadText.gameObject.SetActive(false);

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = 1f;
            staminaSlider.value = 1f;
        }

        if (staminaFill != null)
            staminaFill.color = _staminaColor;
    }

    private void Update()
    {
        UpdateCrosshair();
        UpdateAmmo();
        UpdateHealth();
        UpdateStamina();
        UpdateDrunk();
    }

    private void UpdateCrosshair()
    {
        if (crosshairDot == null) return;
        float ratio = _drunk != null ? _drunk.GetDrunkRatio() : 0f;
        float size = Mathf.Lerp(crosshairBaseSize, crosshairMaxSize, ratio);
        crosshairDot.sizeDelta = new Vector2(size, size);
    }

    private void UpdateAmmo()
    {
        if (_weapon == null) return;

        if (ammoText != null)
            ammoText.text = _weapon.IsReloading
                ? "- / -"
                : $"{_weapon.CurrentAmmo} / {_weapon.CylinderSize}";

        if (reloadText != null)
            reloadText.gameObject.SetActive(_weapon.IsReloading);
    }

    private void UpdateHealth()
    {
        if (_health == null || healthSlider == null) return;

        float ratio = _health.GetHealthRatio();
        healthSlider.value = ratio;

        if (healthFill == null) return;

        healthFill.color = ratio > 0.6f
            ? _healthHigh
            : ratio > 0.3f
                ? Color.Lerp(_healthMid, _healthHigh, (ratio - 0.3f) / 0.3f)
                : Color.Lerp(_healthLow, _healthMid, ratio / 0.3f);
    }

    private void UpdateStamina()
    {
        if (player == null) return;

        if (player.IsSprinting && _currentStamina > 0f)
        {
            _currentStamina = Mathf.Max(0f, _currentStamina - staminaDrainRate * Time.deltaTime);
            _staminaRegenTimer = 0f;
        }
        else if (!player.IsSprinting)
        {
            _staminaRegenTimer += Time.deltaTime;
            if (_staminaRegenTimer >= staminaRegenDelay)
                _currentStamina = Mathf.Min(staminaMax, _currentStamina + staminaRegenRate * Time.deltaTime);
        }

        if (staminaSlider != null)
            staminaSlider.value = _currentStamina / staminaMax;
    }

    private void UpdateDrunk()
    {
        if (_drunk == null || drunkSlider == null) return;

        float ratio = _drunk.GetDrunkRatio();
        drunkSlider.value = ratio;

        if (drunkFill != null)
            drunkFill.color = Color.Lerp(_drunkLow, _drunkHigh, ratio);
    }
}