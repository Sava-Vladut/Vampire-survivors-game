using UnityEngine;

public enum RecentlyHitDefenseStat
{
    Armor = 0,
    Evasion = 1,
}

[DisallowMultipleComponent]
public sealed class RecentlyHitDefenseAccessory : AccessoryBehaviour, IPlayerDefenseIncreaseProvider
{
    [SerializeField] private RecentlyHitDefenseStat stat;
    [SerializeField, Min(0f), Tooltip("1 = 100% increased Armor or Evasion while active.")]
    private float increasedPercent = 1f;
    [SerializeField, Min(0f)] private float recentHitDuration = 3f;

    private SimpleHealth ownerHealth;
    private float activeUntil = float.NegativeInfinity;
    private int lastDisplayedTenth = -1;
    private bool wasActive;

    public bool IsActive => isActiveAndEnabled && Time.time < activeUntil;
    public float RemainingDuration => IsActive ? Mathf.Max(0f, activeUntil - Time.time) : 0f;
    public float ArmorIncrease => stat == RecentlyHitDefenseStat.Armor && IsActive ? increasedPercent : 0f;
    public float EvasionIncrease => stat == RecentlyHitDefenseStat.Evasion && IsActive ? increasedPercent : 0f;

    protected override void OnAccessoryEnabled()
    {
        activeUntil = float.NegativeInfinity;
        wasActive = false;
        lastDisplayedTenth = -1;
        ownerHealth = OwnerHealth;
        if (ownerHealth != null)
        {
            ownerHealth.RegisterDefenseIncreaseProvider(this);
            ownerHealth.DamageTaken -= OnOwnerDamaged;
            ownerHealth.DamageTaken += OnOwnerDamaged;
        }
        RefreshDescription(true);
    }

    protected override void OnAccessoryDisabled()
    {
        if (ownerHealth != null)
        {
            ownerHealth.DamageTaken -= OnOwnerDamaged;
            ownerHealth.UnregisterDefenseIncreaseProvider(this);
        }
        ownerHealth = null;
        activeUntil = float.NegativeInfinity;
        wasActive = false;
    }

    private void Update()
    {
        bool active = IsActive;
        if (wasActive && !active)
            ownerHealth?.NotifyDefenseModifiersChanged();
        wasActive = active;
        RefreshDescription();
    }

    private void OnOwnerDamaged(SimpleHealth.DamageReportEntry entry)
    {
        if (entry.Target != ownerHealth || entry.Amount <= 0 || !entry.WasMitigatable)
            return;

        activeUntil = Time.time + Mathf.Max(0f, recentHitDuration);
        wasActive = IsActive;
        ownerHealth.NotifyDefenseModifiersChanged();
        RefreshDescription(true);
    }

    private void RefreshDescription(bool force = false)
    {
        int displayedTenth = IsActive ? Mathf.CeilToInt(RemainingDuration * 10f) : 0;
        if (!force && displayedTenth == lastDisplayedTenth)
            return;

        lastDisplayedTenth = displayedTenth;
        MarkDescriptionDirty();
    }

    public override string GetAccessoryDescriptionLine()
    {
        string statName = stat == RecentlyHitDefenseStat.Armor ? "Armor" : "Evasion";
        string effectName = stat == RecentlyHitDefenseStat.Armor ? "Reactive Plating" : "Survival Instinct";
        string state = IsActive ? $"Active: {RemainingDuration:0.0}s" : "Ready";
        return $"<color=#FFD166>{effectName}: +{increasedPercent * 100f:0}% {statName} for {recentHitDuration:0.#}s after taking damage ({state})</color>";
    }
}
