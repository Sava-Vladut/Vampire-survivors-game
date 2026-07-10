using System.Collections.Generic;
using UnityEngine;

//
// Handles a single accessory upgrade entry. Applies a player stat bonus via
// SimpleHealth when its GameObject is activated (via PowerUpChooser.TryChoosePowerUp).
// Offers are rolled at runtime by RandomUpgradeGenerator through RandomizeAsOffer.
//
public class AccessoriesUpgrades : MonoBehaviour, IPowerUpSelectionEffect, IPowerUpOfferEligibility
{
    /// <summary>Maximum number of upgrades a single accessory can receive.</summary>
    public const int MaxUpgrades = 20;

    // Serialized by value into scenes/prefabs — do NOT reorder or remove members.
    public enum StatUpgradeType
    {
        None,
        MaxHealthFlat,
        MaxHealthPercent,
        RegenFlat,
        ArmorFlat,
        ArmorPercent,
        EvasionFlat,
        EvasionPercent,
        FireResist,
        ColdResist,
        LightningResist,
        PoisonResist,
        MoveSpeedFlat,
        DashDistanceFlat,
        ThornsFlat,
        ProjectileCountFlat,
        CooldownReduction,
        AttackSpeedPercent,
        GlobalDamagePercent,
        CriticalChanceFlat,
        CriticalDamageFlat,
        WeaponAreaPercent,
        ProjectileSpeedPercent,
        ProjectileLifetimePercent,
        ProjectilePenetrationFlat,
        KnockbackStrengthFlat,
        PickupRadiusFlat,
        XpGainPercent,
        HealingReceivedPercent,
        StatusDurationPercent,
        StatusApplicationChanceFlat,
        DashCooldownReduction,
        AdditionalDashChargeFlat,
        DashInvulnerabilityFlat,
        ContactDamageReduction,
        EnemySlowAura,
    }

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Header("Upgrade Settings")]
    public StatUpgradeType upgradeType = StatUpgradeType.None;

    [Tooltip("Flat amount, or a fraction for percent-based types (e.g., 0.25 = 25%).")]
    public float value = 0f;

    [SerializeField, HideInInspector] private bool hasApplied;
    public bool HasApplied => hasApplied;

    // ---------------------- Apply ----------------------

    public void ApplyUpgrade()
    {
        if (hasApplied) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Accessory owner = GetComponentInParent<Accessory>(true);
        if (player == null || owner == null) return;
        var selection = new PowerUpSelectionContext(null, Upgrade, gameObject, player.transform);
        hasApplied = TryApplyUpgrade(new AccessoryEquipContext(selection, owner, player.transform));
        if (hasApplied) owner.MarkChanged();
    }

    public bool TryApply(PowerUpSelectionContext context)
    {
        if (hasApplied) return true;
        Accessory owner = GetComponentInParent<Accessory>(true);
        if (owner == null || context.PlayerRoot == null) return false;
        hasApplied = TryApplyUpgrade(new AccessoryEquipContext(context, owner, context.PlayerRoot));
        if (hasApplied) owner.MarkChanged();
        return hasApplied;
    }

    public bool CanOffer(PowerUpSelectionContext context)
    {
        Accessory owner = GetComponentInParent<Accessory>(true);
        if (owner == null || !owner.IsEquipped || !owner.isActiveAndEnabled ||
            context.PlayerRoot == null ||
            (owner.transform != context.PlayerRoot && !owner.transform.IsChildOf(context.PlayerRoot)) ||
            context.Chooser == null ||
            !context.Chooser.CanBenefitFromAccessoryUpgrade(upgradeType))
        {
            return false;
        }

        var accessoryContext = new AccessoryEquipContext(context, owner, context.PlayerRoot);
        return AccessoryStatApplicator.CanApply(upgradeType, accessoryContext);
    }

    private bool TryApplyUpgrade(AccessoryEquipContext context)
    {
        if (!AccessoryStatApplicator.CanApply(upgradeType, context)) return false;
        AccessoryStatApplicator.Apply(upgradeType, value, context);
        return true;
    }

    // ---------------------- Random generation ----------------------

    /// <summary>
    /// Configures this component as a freshly rolled upgrade offer: picks a random
    /// stat type (minus excludeTypes and percent types without a base stat), rolls
    /// a value, and creates a new PowerUp entry whose powerUpObject is this
    /// GameObject, so selecting it in the UI activates this object and applies it.
    /// Returns false when no stat type is available.
    /// </summary>
    public bool RandomizeAsOffer(ICollection<StatUpgradeType> excludeTypes = null)
    {
        return RandomizeAsOffer(null, excludeTypes);
    }

    public bool RandomizeAsOffer(
        PowerUpChooser chooser,
        ICollection<StatUpgradeType> excludeTypes = null)
    {
        var health = FindPlayerHealth();
        Accessory owner = GetComponentInParent<Accessory>(true);
        AccessoryUpgradeProfile profile = owner != null ? owner.UpgradeProfile : null;

        var allowed = new List<StatUpgradeType>();
        foreach (StatUpgradeType t in System.Enum.GetValues(typeof(StatUpgradeType)))
        {
            if (t == StatUpgradeType.None) continue;
            if (excludeTypes != null && excludeTypes.Contains(t)) continue;
            if (profile != null ? !profile.Allows(t) : !AccessoryUpgradeProfile.IsDefaultType(t)) continue;
            if (chooser != null && !chooser.CanBenefitFromAccessoryUpgrade(t)) continue;
            // Percent types are useless without a base stat to scale
            if (t == StatUpgradeType.ArmorPercent && (health == null || health.armor <= 0f)) continue;
            if (t == StatUpgradeType.EvasionPercent && (health == null || health.evasion <= 0f)) continue;
            allowed.Add(t);
        }
        if (allowed.Count == 0) return false;

        GeneratedUpgradeSettings settings = GeneratedUpgradeSettings.Load();
        upgradeType = settings != null
            ? settings.PickAccessoryUpgrade(allowed)
            : allowed[Random.Range(0, allowed.Count)];
        if (upgradeType == StatUpgradeType.None)
            return false;

        PowerUpRarity rarity = PowerUp.RollRandomRarity();
        value = GetRandomValueForType(upgradeType) * PowerUp.GetRarityMultiplier(rarity);

        Upgrade = new PowerUp
        {
            powerUpObject = gameObject,
            IsAccessory = true,
            IsUpgrade = true,
            rarity = rarity
        };
        TryAssignIconFromParent();
        SetUpgradeInfo();
        return true;
    }

    private static float GetRandomValueForType(StatUpgradeType t)
    {
        if (GeneratedUpgradeSettings.TryRollAccessory(t, out float configuredValue))
            return configuredValue;

        switch (t)
        {
            case StatUpgradeType.MaxHealthFlat: return Mathf.Round(Random.Range(15f, 60f));
            case StatUpgradeType.MaxHealthPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.RegenFlat: return Random.Range(0.10f, 1.50f);
            case StatUpgradeType.ArmorFlat: return Mathf.Round(Random.Range(1f, 6f));
            case StatUpgradeType.ArmorPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.EvasionFlat: return Mathf.Round(Random.Range(2f, 12f));
            case StatUpgradeType.EvasionPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.FireResist:
            case StatUpgradeType.ColdResist:
            case StatUpgradeType.LightningResist:
            case StatUpgradeType.PoisonResist: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.MoveSpeedFlat: return Random.Range(0.15f, 0.75f);
            case StatUpgradeType.DashDistanceFlat: return Random.Range(0.25f, 1.50f);
            case StatUpgradeType.ThornsFlat: return Mathf.Round(Random.Range(2f, 12f));
            case StatUpgradeType.ProjectileCountFlat: return 1f;
            case StatUpgradeType.CooldownReduction: return Random.Range(0.03f, 0.12f);
            case StatUpgradeType.AttackSpeedPercent: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.GlobalDamagePercent: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.CriticalChanceFlat: return Random.Range(0.02f, 0.10f);
            case StatUpgradeType.CriticalDamageFlat: return Random.Range(0.10f, 0.50f);
            case StatUpgradeType.WeaponAreaPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.ProjectileSpeedPercent: return Random.Range(0.05f, 0.30f);
            case StatUpgradeType.ProjectileLifetimePercent: return Random.Range(0.05f, 0.30f);
            case StatUpgradeType.ProjectilePenetrationFlat: return 1f;
            case StatUpgradeType.KnockbackStrengthFlat: return Random.Range(0.50f, 2.50f);
            case StatUpgradeType.PickupRadiusFlat: return Random.Range(0.50f, 3f);
            case StatUpgradeType.XpGainPercent: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.HealingReceivedPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.StatusDurationPercent: return Random.Range(0.10f, 0.40f);
            case StatUpgradeType.StatusApplicationChanceFlat: return Random.Range(0.03f, 0.12f);
            case StatUpgradeType.DashCooldownReduction: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.AdditionalDashChargeFlat: return 1f;
            case StatUpgradeType.DashInvulnerabilityFlat: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.ContactDamageReduction: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.EnemySlowAura: return Random.Range(0.05f, 0.20f);
        }
        return 0f;
    }

    // ---------------------- Display text ----------------------

    private const string NumColor = "#8888FF";
    private static string C(string s) => $"<color={NumColor}>{s}</color>";

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;

        string flat = C(Mathf.RoundToInt(value).ToString());
        string pct = C((value * 100f).ToString("F0")) + "%";
        string perSec = C(value.ToString("F2")) + "/s";
        string decimalValue = C(value.ToString("F2"));

        (string title, string desc) = upgradeType switch
        {
            StatUpgradeType.MaxHealthFlat => ($"Vitality +{flat}", $"Gain {flat} maximum health and become harder to slay."),
            StatUpgradeType.MaxHealthPercent => ($"Greater Vitality +{pct}", $"Increase your current maximum health by {pct}."),
            StatUpgradeType.RegenFlat => ($"Trollblood +{perSec}", $"Restore an additional {perSec} health every second."),
            StatUpgradeType.ArmorFlat => ($"Iron Skin +{flat}", $"Gain {flat} armor to blunt incoming attacks."),
            StatUpgradeType.ArmorPercent => ($"Hardened Armor +{pct}", $"Increase your current armor by {pct}."),
            StatUpgradeType.EvasionFlat => ($"Nimble Footwork +{flat}", $"Gain {flat} evasion, improving your chance to avoid attacks."),
            StatUpgradeType.EvasionPercent => ($"Elusive Form +{pct}", $"Increase your current evasion by {pct}."),
            StatUpgradeType.FireResist => ($"Flame Ward +{pct}", $"Take {pct} less fire damage."),
            StatUpgradeType.ColdResist => ($"Frost Ward +{pct}", $"Take {pct} less cold damage."),
            StatUpgradeType.LightningResist => ($"Storm Ward +{pct}", $"Take {pct} less lightning damage."),
            StatUpgradeType.PoisonResist => ($"Venom Ward +{pct}", $"Take {pct} less poison damage."),
            StatUpgradeType.MoveSpeedFlat => ($"Fleetfoot +{decimalValue}", $"Move {decimalValue} faster at all times."),
            StatUpgradeType.DashDistanceFlat => ($"Bounding Step +{decimalValue}", $"Dash and blink {decimalValue} farther."),
            StatUpgradeType.ThornsFlat => ($"Spiked Plate +{flat}", $"Retaliate for {flat} physical damage whenever an enemy wounds you."),
            StatUpgradeType.ProjectileCountFlat => ($"Multishot +{flat}", $"Fire {flat} additional projectile from every ranged weapon."),
            StatUpgradeType.CooldownReduction => ($"Quick Recovery +{pct}", $"Reduce the delay between weapon attacks by {pct}."),
            StatUpgradeType.AttackSpeedPercent => ($"Rapid Assault +{pct}", $"Increase attack and burst speed by {pct}."),
            StatUpgradeType.GlobalDamagePercent => ($"Brutal Force +{pct}", $"Increase all weapon damage by {pct}."),
            StatUpgradeType.CriticalChanceFlat => ($"Keen Eye +{pct}", $"Gain {pct} critical-hit chance."),
            StatUpgradeType.CriticalDamageFlat => ($"Deadly Precision +{pct}", $"Critical hits deal {pct} additional damage."),
            StatUpgradeType.WeaponAreaPercent => ($"Expansive Reach +{pct}", $"Increase weapon area, projectile size, and explosion radius by {pct}."),
            StatUpgradeType.ProjectileSpeedPercent => ($"High Velocity +{pct}", $"Increase projectile speed by {pct}."),
            StatUpgradeType.ProjectileLifetimePercent => ($"Lingering Shots +{pct}", $"Increase projectile lifetime by {pct}."),
            StatUpgradeType.ProjectilePenetrationFlat => ($"Piercing Rounds +{flat}", $"Projectiles pass through {flat} additional target."),
            StatUpgradeType.KnockbackStrengthFlat => ($"Heavy Impact +{decimalValue}", $"Add {decimalValue} knockback strength to weapon hits."),
            StatUpgradeType.PickupRadiusFlat => ($"Far Reach +{decimalValue}", $"Attract pickups from {decimalValue} units farther away."),
            StatUpgradeType.XpGainPercent => ($"Fast Learner +{pct}", $"Gain {pct} more experience."),
            StatUpgradeType.HealingReceivedPercent => ($"Restorative Blood +{pct}", $"Increase all healing received by {pct}."),
            StatUpgradeType.StatusDurationPercent => ($"Lasting Affliction +{pct}", $"Your weapon status effects last {pct} longer."),
            StatUpgradeType.StatusApplicationChanceFlat => ($"Reliable Affliction +{pct}", $"Gain {pct} status-application chance."),
            StatUpgradeType.DashCooldownReduction => ($"Light Step +{pct}", $"Reduce dash recharge time by {pct}."),
            StatUpgradeType.AdditionalDashChargeFlat => ($"Reserve Step +{flat}", $"Store {flat} additional dash charge."),
            StatUpgradeType.DashInvulnerabilityFlat => ($"Phase Step +{decimalValue}s", $"Remain invulnerable for {decimalValue} seconds after dashing."),
            StatUpgradeType.ContactDamageReduction => ($"Impact Guard +{pct}", $"Take {pct} less damage from direct enemy contact."),
            StatUpgradeType.EnemySlowAura => ($"Dread Presence +{pct}", $"Slow nearby enemies by {pct}."),
            _ => ("No Upgrade", "This upgrade slot is empty."),
        };

        // Prefix with the owning accessory's name
        var owner = GetComponentInParent<Accessory>(true);
        string ownerName = owner != null && !string.IsNullOrWhiteSpace(owner.DisplayName)
            ? owner.DisplayName
            : (transform.parent != null ? transform.parent.name : null);

        Upgrade.powerUpName = string.IsNullOrEmpty(ownerName) ? title : $"{ownerName} - {title}";
        Upgrade.powerUpDescription = desc;
    }

    private void TryAssignIconFromParent()
    {
        if (Upgrade == null) return;

        var owner = GetComponentInParent<Accessory>(true);
        if (owner != null && owner.Icon != null)
            Upgrade.powerUpIcon = owner.Icon;
    }

    // ---------------------- Helpers ----------------------

    private SimpleHealth FindPlayerHealth()
    {
        var inParent = GetComponentInParent<SimpleHealth>(true);
        if (inParent != null) return inParent;

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponentInChildren<SimpleHealth>() : null;
    }

}
