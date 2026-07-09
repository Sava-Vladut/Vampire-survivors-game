using UnityEngine;

public class MissingHealthDamageAccessory : AccessoryBehaviour, IPlayerDamageMultiplierProvider
{
    [SerializeField, Tooltip("Missing max-health fraction needed for each damage step. 0.10 = every missing 10% health.")]
    private float missingHealthStep = 0.10f;
    [SerializeField, Tooltip("Damage bonus gained per missing-health step. 0.02 = +2%.")]
    private float bonusPerMissingStep = 0.02f;
    [SerializeField, Tooltip("Maximum bonus damage. 0.20 = +20%.")]
    private float maxBonusDamage = 0.20f;
    [SerializeField, Min(0.01f)] private float refreshInterval = 0.10f;

    private SimpleHealth health;
    private float lastDisplayedBonus = -1f;
    private float nextRefreshTime;

    public float CurrentBonusDamage => CalculateBonusDamage();
    public float DamageMultiplier => 1f + CurrentBonusDamage;

    protected override void OnAccessoryEnabled()
    {
        health = GetComponentInParent<SimpleHealth>(true);
        RefreshDescriptionIfNeeded(true);
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime)
            return;

        nextRefreshTime = Time.time + refreshInterval;
        RefreshDescriptionIfNeeded();
    }

    private float CalculateBonusDamage()
    {
        if (health == null)
            health = GetComponentInParent<SimpleHealth>(true);

        if (health == null || health.MaxHealth <= 0 || missingHealthStep <= 0f)
            return 0f;

        float healthFraction = Mathf.Clamp01(health.currentHealth / health.MaxHealth);
        float missingFraction = 1f - healthFraction;
        int missingSteps = Mathf.FloorToInt(missingFraction / missingHealthStep);
        return Mathf.Min(missingSteps * bonusPerMissingStep, maxBonusDamage);
    }

    private void RefreshDescriptionIfNeeded(bool force = false)
    {
        float current = CurrentBonusDamage;
        if (!force && Mathf.Approximately(current, lastDisplayedBonus))
            return;

        lastDisplayedBonus = current;
        MarkDescriptionDirty();
    }

    public override string GetAccessoryDescriptionLine()
    {
        float missingPercent = health != null && health.MaxHealth > 0
            ? Mathf.Clamp01(1f - (health.currentHealth / health.MaxHealth)) * 100f
            : 0f;

        return $"<color=#FFD166>Damage: +{CurrentBonusDamage * 100f:F0}% ({missingPercent:F0}% missing HP)</color>";
    }
}
