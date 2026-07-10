// DamagePopup2D.cs
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class DamagePopup2D : MonoBehaviour
{
    public float lifetime = 0.8f;
    public float floatSpeed = 1.5f;     // units/sec upward
    public float fadeStart = 0.3f;      // seconds before end to start fading

    [Header("Critical Hits")]
    [SerializeField, Min(1f)] private float criticalSizeMultiplier = 1.5f;

    private TMP_Text tmp;
    private float age;
    private Vector3 normalTextScale;
    private int accumulatedDamage;
    private bool hasDamage;
    private bool containsCriticalHit;

    void Awake()
    {
        if (!TryCacheText()) { Debug.LogWarning("DamagePopup2D: no TextMeshProUGUI found."); enabled = false; return; }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;

        // move up
        transform.position += Vector3.up * floatSpeed * dt;

        // fade near the end
        float tLeft = lifetime - age;
        if (tLeft <= fadeStart)
        {
            float a = Mathf.Clamp01(tLeft / Mathf.Max(0.0001f, fadeStart));
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, a);
        }

        if (age >= lifetime) Destroy(gameObject);
    }

    public void SetText(string text)
    {
        if (!TryCacheText())
            return;

        hasDamage = false;
        containsCriticalHit = false;
        ApplyCriticalSize(false);
        tmp.text = text;
    }

    public void SetDamage(int damage, Color color, bool isCritical)
    {
        if (!TryCacheText())
            return;

        accumulatedDamage = Mathf.Max(0, damage);
        hasDamage = true;
        containsCriticalHit = isCritical;
        tmp.color = color;
        RefreshDamageText();
    }

    public void AddDamage(int damage, Color color, bool isCritical)
    {
        if (!hasDamage)
        {
            SetDamage(damage, color, isCritical);
            return;
        }

        accumulatedDamage += Mathf.Max(0, damage);
        containsCriticalHit |= isCritical;
        tmp.color = color;
        RefreshDamageText();
    }

    public void SetStatusAffliction(StatusEffectSystem.StatusType statusType)
    {
        if (!TryGetAfflictionPopup(statusType, out string text, out Color color))
            return;

        if (!TryCacheText())
            return;

        tmp.text = text;
        tmp.color = color;
        ApplyCriticalSize(false);
    }

    public static bool TryGetAfflictionPopup(StatusEffectSystem.StatusType statusType, out string text, out Color color)
    {
        switch (statusType)
        {
            case StatusEffectSystem.StatusType.Bleeding:
                text = "Bleeding!";
                color = new Color(1f, 0.2f, 0.2f);
                return true;
            case StatusEffectSystem.StatusType.Stun:
                text = "Stunned!";
                color = new Color(1f, 0.95f, 0.35f);
                return true;
            case StatusEffectSystem.StatusType.Ignite:
                text = "Ignited!";
                color = new Color(1f, 0.35f, 0f);
                return true;
            case StatusEffectSystem.StatusType.Shock:
                text = "Shocked!";
                color = new Color(1f, 1f, 0.25f);
                return true;
            case StatusEffectSystem.StatusType.Poison:
                text = "Poisoned!";
                color = new Color(0.45f, 1f, 0.35f);
                return true;
            case StatusEffectSystem.StatusType.Frozen:
                text = "Frozen!";
                color = new Color(0.35f, 0.8f, 1f);
                return true;
            case StatusEffectSystem.StatusType.Slow:
                text = "Slowed!";
                color = new Color(0.55f, 0.7f, 1f);
                return true;
            case StatusEffectSystem.StatusType.Fear:
                text = "Feared!";
                color = new Color(0.75f, 0.45f, 1f);
                return true;
            case StatusEffectSystem.StatusType.Cursed:
                text = "Cursed!";
                color = new Color(0.85f, 0.25f, 1f);
                return true;
            default:
                text = null;
                color = Color.white;
                return false;
        }
    }

    private bool TryCacheText()
    {
        if (tmp != null)
            return true;

        tmp = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>();
        if (tmp != null)
            normalTextScale = tmp.transform.localScale;

        return tmp != null;
    }

    private void RefreshDamageText()
    {
        tmp.text = containsCriticalHit ? $"{accumulatedDamage}!" : accumulatedDamage.ToString();
        ApplyCriticalSize(containsCriticalHit);
    }

    private void ApplyCriticalSize(bool isCritical)
    {
        if (tmp == null)
            return;

        tmp.transform.localScale = normalTextScale * (isCritical ? criticalSizeMultiplier : 1f);
    }
}
