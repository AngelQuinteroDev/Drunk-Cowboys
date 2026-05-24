using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(HealthSystem))]
public class DummyTarget : MonoBehaviour
{
    [Header("Feedback")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color normalColor = new Color(0.8f, 0.7f, 0.5f);
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.12f;

    [Header("World Space Health Display")]
    [SerializeField] private TextMeshPro healthLabel;
    [SerializeField] private Transform labelAnchor;

    [Header("Auto Reset")]
    [SerializeField] private bool autoReset = true;
    [SerializeField] private float resetDelay = 3f;
    [SerializeField] private float maxHealth = 100f;

    private HealthSystem _health;
    private MaterialPropertyBlock _mpb;
    private float _flashTimer;
    private bool _isFlashing;
    private float _resetTimer;
    private bool _isDead;
    private Camera _cam;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _mpb = new MaterialPropertyBlock();
        _cam = Camera.main;

        SetBodyColor(normalColor);

        _health.OnHealthChanged.AddListener(OnHealthChanged);
        _health.OnDeath.AddListener(OnDeath);
    }

    private void Update()
    {
        if (_isFlashing)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _isFlashing = false;
                SetBodyColor(normalColor);
            }
        }

        if (_isDead && autoReset)
        {
            _resetTimer -= Time.deltaTime;
            if (_resetTimer <= 0f)
                ResetDummy();
        }

        if (labelAnchor != null && _cam != null)
            labelAnchor.LookAt(_cam.transform);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (healthLabel != null)
            healthLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

        Flash();
    }

    private void OnDeath()
    {
        _isDead = true;
        _resetTimer = resetDelay;

        SetBodyColor(Color.black);

        if (healthLabel != null)
            healthLabel.text = "DEAD";
    }

    private void Flash()
    {
        _isFlashing = true;
        _flashTimer = flashDuration;
        SetBodyColor(hitColor);
    }

    private void SetBodyColor(Color color)
    {
        if (bodyRenderer == null) return;
        _mpb.SetColor("_BaseColor", color);
        _mpb.SetColor("_Color", color);
        bodyRenderer.SetPropertyBlock(_mpb);
    }

    private void ResetDummy()
    {
        _isDead = false;
        _health.Heal(maxHealth);
        SetBodyColor(normalColor);

        if (healthLabel != null)
            healthLabel.text = $"{maxHealth} / {maxHealth}";
    }
}