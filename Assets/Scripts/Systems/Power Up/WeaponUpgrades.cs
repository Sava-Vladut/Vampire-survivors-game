using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serialized compatibility component for one applied weapon upgrade. Behavior,
/// presentation, eligibility, and roll ranges live in target-specific definition
/// modules registered by WeaponUpgradeCatalog.
/// </summary>
public class WeaponUpgrades : MonoBehaviour, IPowerUpSelectionEffect, IPowerUpOfferEligibility
{
    // Serialized by value into scenes/settings. Do not reorder or remove values.
    public enum UpgradeType
    {
        None,

        KnifeDamageFlat,
        KnifeDamagePercent,
        KnifeDamageTypeIndex,
        KnifeRadiusFlat,
        KnifeRadiusPercent,
        KnifeMaxTargetsFlat,
        KnifeLifestealFlat,
        KnifeLifestealPercent,
        KnifeCritChanceFlat,
        KnifeCritMultiplierFlat,
        KnifeSplashRadiusFlat,
        KnifeSplashRadiusPercent,
        KnifeSplashDamagePercentFlat,
        KnifeSplashDamagePercentPercent,
        KnifeStatusApplyChanceFlat,
        KnifeStatusApplyChancePercent,
        KnifeStatusDurationFlat,
        KnifeStatusDurationPercent,
        KnifeEnableStatusEffect,
        KnifeStatusEffectIndex,
        KnifeKnockbackFlat,
        KnifeCullThreshold,

        ShooterDamageFlat,
        ShooterDamagePercent,
        ShooterDamageTypeIndex,
        ShooterProjectileCount,
        ShooterSpreadAngleFlat,
        ShooterSpreadAnglePercent,
        ShooterProjectileSpeedFlat,
        ShooterProjectileSpeedPercent,
        ShooterLifetimeFlat,
        ShooterLifetimePercent,
        ShooterCritChanceFlat,
        ShooterCritMultiplierFlat,
        ShooterStatusApplyChanceFlat,
        ShooterStatusApplyChancePercent,
        ShooterStatusDurationFlat,
        ShooterStatusDurationPercent,
        ShooterEnableStatusEffect,
        ShooterStatusEffectIndex,
        ShooterKnockbackFlat,
        ShooterCullThreshold,
        ShooterChainHits,

        TickRateFlat,
        TickRatePercent,
        UnusedTickUpgrade2,
        UnusedTickUpgrade3,
        UnusedTickUpgrade4,
        UnusedTickUpgrade5,

        KnifeEchoStrikeChance,
        ShooterForkShotChance,
        ShooterPenetrationFlat
    }

    public const int MaxUpgrades = 20;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;

    [Tooltip("Flat amount, integer count, enum index, or percentage fraction depending on the definition.")]
    public float value;

    [SerializeField, HideInInspector] private bool seedStatusApplyChance;
    [SerializeField, HideInInspector] private float seededStatusApplyChance;
    [SerializeField, HideInInspector] private bool hasApplied;

    public bool HasApplied => hasApplied;

    private void Awake()
    {
        NormalizeTypeForTarget();
        RefreshPresentation();
    }

    private void OnValidate()
    {
        NormalizeTypeForTarget();
        RefreshPresentation();
    }

    public static bool IsGeneratedType(UpgradeType type) => WeaponUpgradeCatalog.IsGenerated(type);

    public static bool TryGetTarget(UpgradeType type, out WeaponUpgradeTarget target)
    {
        if (WeaponUpgradeCatalog.TryGet(type, out WeaponUpgradeDefinition definition))
        {
            target = definition.Target;
            return true;
        }

        target = default;
        return false;
    }

    public bool RandomizeAsOffer(ICollection<UpgradeType> excludeTypes = null)
    {
        Transform target = DefaultTarget;
        var candidates = new List<WeaponUpgradeDefinition>();
        IReadOnlyList<WeaponUpgradeDefinition> definitions = WeaponUpgradeCatalog.All;

        for (int i = 0; i < definitions.Count; i++)
        {
            WeaponUpgradeDefinition definition = definitions[i];
            if (!definition.IsGenerated || !definition.CanOffer(target)) continue;
            if (excludeTypes != null && excludeTypes.Contains(definition.Type)) continue;
            candidates.Add(definition);
        }

        if (candidates.Count == 0)
            return false;

        WeaponUpgradeDefinition selected = candidates[Random.Range(0, candidates.Count)];
        PowerUpRarity rarity = PowerUp.RollRandomRarity();
        float rolledValue = selected.RollBaseValue(target);
        return ConfigureAsOffer(selected.Type, rolledValue, rarity, applyRarityMultiplier: true);
    }

    public bool ConfigureAsOffer(
        UpgradeType type,
        float rolledValue,
        PowerUpRarity rarity,
        bool applyRarityMultiplier = true)
    {
        if (!WeaponUpgradeCatalog.TryGet(type, out WeaponUpgradeDefinition definition))
            return false;

        Transform target = ResolveTarget(definition);
        if (!definition.Supports(target))
            return false;

        upgradeType = type;
        value = applyRarityMultiplier && definition.ScalesWithRarity
            ? rolledValue * PowerUp.GetRarityMultiplier(rarity)
            : rolledValue;

        Upgrade = new PowerUp
        {
            powerUpObject = gameObject,
            IsWeapon = true,
            IsUpgrade = true,
            rarity = rarity,
            weight = 1f
        };

        RefreshPresentation();
        return true;
    }

    public void ConfigureStatusChanceSeed(float baseChance)
    {
        seedStatusApplyChance = true;
        seededStatusApplyChance = Mathf.Clamp01(baseChance);
    }

    public void ApplyUpgrade()
    {
        if (!hasApplied)
            hasApplied = TryApplyUpgrade();
    }

    public bool TryApply(PowerUpSelectionContext context)
    {
        if (hasApplied) return true;
        hasApplied = TryApplyUpgrade();
        return hasApplied;
    }

    public bool CanOffer(PowerUpSelectionContext context)
    {
        if (upgradeType == UpgradeType.None || context.PlayerRoot == null ||
            !WeaponUpgradeCatalog.TryGet(upgradeType, out WeaponUpgradeDefinition definition))
        {
            return false;
        }

        Transform target = ResolveTarget(definition);
        if (!IsActivePlayerTarget(target, context.PlayerRoot, definition))
            return false;

        if (definition.CanOffer(target))
            return true;

        // FirstOnHitStatusOfferSource deliberately creates this offer before the
        // target satisfies the normal status-related eligibility predicate. The
        // seeded offer is useful because selecting it grants that first status.
        return seedStatusApplyChance && definition.IsFirstOnHit && definition.Supports(target);
    }

    public void RefreshPresentation()
    {
        if (Upgrade == null) return;

        if (!WeaponUpgradeCatalog.TryGet(upgradeType, out WeaponUpgradeDefinition definition))
        {
            Upgrade.powerUpName = "No Upgrade";
            Upgrade.powerUpDescription = "This upgrade slot is empty.";
            return;
        }

        Transform target = ResolveTarget(definition);
        definition.BuildText(target, value, out string title, out string description);
        if (seedStatusApplyChance && definition.IsFirstOnHit)
        {
            description +=
                $" Starts with {Mathf.RoundToInt(Mathf.Clamp01(seededStatusApplyChance) * 100f)}% base chance.";
        }
        Upgrade.powerUpName = title;
        Upgrade.powerUpDescription = description;

        Sprite icon = definition.GetIcon(target);
        if (icon != null)
            Upgrade.powerUpIcon = icon;
    }

    private bool TryApplyUpgrade()
    {
        if (upgradeType == UpgradeType.None ||
            !WeaponUpgradeCatalog.TryGet(upgradeType, out WeaponUpgradeDefinition definition))
            return false;

        Transform target = ResolveTarget(definition);
        var application = new WeaponUpgradeApplication(
            target,
            value,
            seedStatusApplyChance,
            seededStatusApplyChance);

        return definition.TryApply(application);
    }

    private void NormalizeTypeForTarget()
    {
        if (upgradeType == UpgradeType.None) return;

        if (!WeaponUpgradeCatalog.TryGet(upgradeType, out WeaponUpgradeDefinition definition) ||
            !definition.Supports(ResolveTarget(definition)))
        {
            upgradeType = UpgradeType.None;
        }
    }

    private Transform ResolveTarget(WeaponUpgradeDefinition definition)
    {
        if (transform.parent != null && definition.Supports(transform.parent))
            return transform.parent;
        if (definition.Supports(transform))
            return transform;
        return DefaultTarget;
    }

    private Transform DefaultTarget => transform.parent != null ? transform.parent : transform;

    private static bool IsActivePlayerTarget(
        Transform target,
        Transform playerRoot,
        WeaponUpgradeDefinition definition)
    {
        if (target == null || playerRoot == null || !target.gameObject.activeInHierarchy ||
            (target != playerRoot && !target.IsChildOf(playerRoot)))
        {
            return false;
        }

        return definition.Target switch
        {
            WeaponUpgradeTarget.Knife => target.TryGetComponent(out Knife knife) && knife.isActiveAndEnabled,
            WeaponUpgradeTarget.Shooter => target.TryGetComponent(out SimpleShooter shooter) && shooter.isActiveAndEnabled,
            WeaponUpgradeTarget.WeaponTick => target.TryGetComponent(out WeaponTick tick) && tick.isActiveAndEnabled,
            _ => false,
        };
    }
}
