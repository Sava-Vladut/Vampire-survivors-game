using UnityEngine;

public class MomentumHourglassAccessory : AccessoryBehaviour, IPlayerDamageMultiplierProvider
{
    [SerializeField, Min(0f), Tooltip("Damage gained per second without taking damage. 0.01 = +1% per second.")]
    private float bonusPerSecond = 0.01f;
    [SerializeField, Min(0f), Tooltip("Maximum bonus damage. 0.50 = +50%.")]
    private float maxBonusDamage = 0.50f;
    [SerializeField, Min(0.01f)] private float descriptionRefreshInterval = 0.20f;

    private SimpleHealth ownerHealth;
    private float untouchedSeconds;
    private int lastDisplayedPercent = -1;
    private float nextDescriptionRefreshTime;

    public float CurrentBonusDamage => Mathf.Min(untouchedSeconds * bonusPerSecond, maxBonusDamage);
    public float DamageMultiplier => 1f + CurrentBonusDamage;

    protected override void OnAccessoryEnabled()
    {
        untouchedSeconds = 0f;
        ownerHealth = GetComponentInParent<SimpleHealth>(true);
        if (ownerHealth != null)
            ownerHealth.DamageTaken += OnOwnerDamaged;

        RefreshDescriptionIfNeeded(true);
    }

    protected override void OnAccessoryDisabled()
    {
        if (ownerHealth != null)
            ownerHealth.DamageTaken -= OnOwnerDamaged;
    }

    private void Update()
    {
        untouchedSeconds += Time.deltaTime;

        if (Time.time < nextDescriptionRefreshTime)
            return;

        nextDescriptionRefreshTime = Time.time + descriptionRefreshInterval;
        RefreshDescriptionIfNeeded();
    }

    private void OnOwnerDamaged(SimpleHealth.DamageReportEntry entry)
    {
        if (entry.Amount <= 0)
            return;

        untouchedSeconds = 0f;
        RefreshDescriptionIfNeeded(true);
    }

    private void RefreshDescriptionIfNeeded(bool force = false)
    {
        int displayedPercent = Mathf.FloorToInt(CurrentBonusDamage * 100f);
        if (!force && displayedPercent == lastDisplayedPercent)
            return;

        lastDisplayedPercent = displayedPercent;
        MarkDescriptionDirty();
    }

    public override string GetAccessoryDescriptionLine()
    {
        return $"<color=#FFD166>Damage: +{CurrentBonusDamage * 100f:F0}% ({untouchedSeconds:F0}s untouched)</color>";
    }
}
