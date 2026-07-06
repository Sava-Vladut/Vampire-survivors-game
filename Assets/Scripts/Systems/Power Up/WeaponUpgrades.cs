using System.Collections.Generic;
using UnityEngine;
//
// Handles a single weapon upgrade entry. Applies its effect to the parent weapon
// when its GameObject is activated (via PowerUpChooser.TryChoosePowerUp).
// Offers are rolled at runtime by RandomUpgradeGenerator through RandomizeAsOffer.
//
public class WeaponUpgrades : MonoBehaviour
{
    // Serialized by value into scenes/prefabs — do NOT reorder or remove members.
    // Most members are grouped in contiguous ranges; unique effects are appended
    // to preserve existing serialized enum values (see IsTypeAllowedForParent).
    public enum UpgradeType
    {
        None,

        // --- Knife (KnifeDamageFlat..KnifeCullThreshold) ---
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

        // --- Shooter (ShooterDamageFlat..ShooterChainHits) ---
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

        // --- WeaponTick (TickRateFlat..BurstSpacingPercent) ---
        TickRateFlat,
        TickRatePercent,
        BurstCountFlat,
        BurstCountPercent,
        BurstSpacingFlat,
        BurstSpacingPercent,

        // --- Unique generated effects (appended to preserve serialized enum values) ---
        KnifeEchoStrikeChance,
        ShooterForkShotChance
    }

    /// <summary>Maximum number of upgrades a single weapon can receive.</summary>
    public const int MaxUpgrades = 20;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    // Icon auto-inferred from parent weapon (Knife/SimpleShooter).

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;

    [Tooltip("Acts as integer for flat amounts (rounded), or as a percent when the upgrade type is % based (e.g., 0.25 = 25%).")]
    public float value = 0f;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        TryAssignIconFromParent();

        // Normalize type if not allowed for the current parent
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        SetUpgradeInfo();

        ApplyUpgrade();
    }

    private void OnValidate()
    {
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        TryAssignIconFromParent();
        SetUpgradeInfo();
    }

    // ---------------------- Helpers ----------------------

    private bool HasParent<T>() where T : Component =>
        transform.parent != null && transform.parent.TryGetComponent<T>(out _);

    private static bool InRange(UpgradeType t, UpgradeType first, UpgradeType last) =>
        t >= first && t <= last;

    private static bool IsKnifeType(UpgradeType t) =>
        InRange(t, UpgradeType.KnifeDamageFlat, UpgradeType.KnifeCullThreshold) ||
        t == UpgradeType.KnifeEchoStrikeChance;
    private static bool IsShooterType(UpgradeType t) =>
        InRange(t, UpgradeType.ShooterDamageFlat, UpgradeType.ShooterChainHits) ||
        t == UpgradeType.ShooterForkShotChance;
    private static bool IsTickType(UpgradeType t) => InRange(t, UpgradeType.TickRateFlat, UpgradeType.BurstSpacingPercent);
    private static bool IsStatusType(UpgradeType t) =>
        InRange(t, UpgradeType.KnifeStatusApplyChanceFlat, UpgradeType.KnifeStatusEffectIndex) ||
        InRange(t, UpgradeType.ShooterStatusApplyChanceFlat, UpgradeType.ShooterStatusEffectIndex);
    private static bool IsEnableStatusType(UpgradeType t) =>
        t == UpgradeType.KnifeEnableStatusEffect ||
        t == UpgradeType.ShooterEnableStatusEffect;

    public static bool IsGeneratedType(UpgradeType type) =>
        type != UpgradeType.None && !IsEnableStatusType(type);

    private bool IsTypeAllowedForParent(UpgradeType type)
    {
        if (type == UpgradeType.None) return true;
        if (IsKnifeType(type)) return HasParent<Knife>();
        if (IsShooterType(type)) return HasParent<SimpleShooter>();
        if (IsTickType(type)) return HasParent<WeaponTick>();
        return false;
    }

    // ---------------------- Display text ----------------------

    private enum ValueFormat
    {
        None,       // no value in the text
        Int,        // rounded integer
        Percent,    // value * 100, "F0" + %
        F1,
        F2,
        Seconds1,   // "F1" + s
        Seconds2,   // "F2" + s
        Multiplier, // "F2" + x
        Degrees,    // "F1" + °
        DamageType, // enum name, uncolored
        StatusType  // status enum name, uncolored
    }

    private readonly struct UpgradeText
    {
        public readonly string title;
        public readonly string description;
        public readonly ValueFormat format;

        public UpgradeText(string title, string description, ValueFormat format)
        {
            this.title = title;
            this.description = description;
            this.format = format;
        }
    }

    // Title/description templates; {0} is the formatted value. Knife and Shooter
    // share entries where the wording is identical.
    private static readonly Dictionary<UpgradeType, UpgradeText> upgradeTexts = BuildUpgradeTexts();

    private static Dictionary<UpgradeType, UpgradeText> BuildUpgradeTexts()
    {
        var table = new Dictionary<UpgradeType, UpgradeText>();
        void Add(string title, string desc, ValueFormat format, params UpgradeType[] types)
        {
            foreach (var t in types) table[t] = new UpgradeText(title, desc, format);
        }

        // Shared Knife + Shooter
        Add("Tempered Power +{0}", "Each hit deals {0} more damage.", ValueFormat.Int,
            UpgradeType.KnifeDamageFlat, UpgradeType.ShooterDamageFlat);
        Add("Weapon Mastery +{0}", "All damage from this weapon is increased by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeDamagePercent, UpgradeType.ShooterDamagePercent);
        Add("{0} Infusion", "Attunes every hit from this weapon to {0} damage.", ValueFormat.DamageType,
            UpgradeType.KnifeDamageTypeIndex, UpgradeType.ShooterDamageTypeIndex);
        Add("Keen Edge +{0}", "Grants {0} additional critical-hit chance.", ValueFormat.Percent,
            UpgradeType.KnifeCritChanceFlat, UpgradeType.ShooterCritChanceFlat);
        Add("Savage Criticals +{0}", "Critical hits gain {0} additional damage multiplier.", ValueFormat.Multiplier,
            UpgradeType.KnifeCritMultiplierFlat, UpgradeType.ShooterCritMultiplierFlat);
        Add("Affliction Chance +{0}", "Hits are {0} more likely to inflict their on-hit effect.", ValueFormat.Percent,
            UpgradeType.KnifeStatusApplyChanceFlat, UpgradeType.ShooterStatusApplyChanceFlat);
        Add("Potent Affliction +{0}", "Multiplies this weapon's on-hit effect chance by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeStatusApplyChancePercent, UpgradeType.ShooterStatusApplyChancePercent);
        Add("Lingering Curse +{0}", "On-hit effects remain on enemies for {0} longer.", ValueFormat.Seconds1,
            UpgradeType.KnifeStatusDurationFlat, UpgradeType.ShooterStatusDurationFlat);
        Add("Enduring Affliction +{0}", "Increases this weapon's on-hit effect duration by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeStatusDurationPercent, UpgradeType.ShooterStatusDurationPercent);
        Add("Enable Status On Hit", "Enables inflicting status effects on hit.", ValueFormat.None,
            UpgradeType.KnifeEnableStatusEffect, UpgradeType.ShooterEnableStatusEffect);
        Add("{0} Affliction", "This weapon's hits now inflict {0}.", ValueFormat.StatusType,
            UpgradeType.KnifeStatusEffectIndex, UpgradeType.ShooterStatusEffectIndex);
        Add("Crushing Force +{0}", "Hits knock enemies back with {0} additional force.", ValueFormat.F1,
            UpgradeType.KnifeKnockbackFlat, UpgradeType.ShooterKnockbackFlat);
        Add("Culling Strike +{0}", "Enemies at or below {0} health are instantly slain by this weapon.", ValueFormat.Percent,
            UpgradeType.KnifeCullThreshold, UpgradeType.ShooterCullThreshold);

        // Knife only
        Add("Long Reach +{0}", "Strikes reach {0} farther from the weapon.", ValueFormat.F2, UpgradeType.KnifeRadiusFlat);
        Add("Sweeping Reach +{0}", "Increases this weapon's attack radius by {0}.", ValueFormat.Percent, UpgradeType.KnifeRadiusPercent);
        Add("Cleave +{0}", "Each strike can hit {0} additional enemies.", ValueFormat.Int, UpgradeType.KnifeMaxTargetsFlat);
        Add("Bloodthirst +{0}", "Converts an additional {0} of damage dealt into healing.", ValueFormat.Percent, UpgradeType.KnifeLifestealFlat);
        Add("Greater Bloodthirst +{0}", "Increases this weapon's current lifesteal by {0}.", ValueFormat.Percent, UpgradeType.KnifeLifestealPercent);
        Add("Wider Impact +{0}", "Splash attacks reach {0} farther around the target.", ValueFormat.F2, UpgradeType.KnifeSplashRadiusFlat);
        Add("Expanding Impact +{0}", "Increases splash radius by {0}.", ValueFormat.Percent, UpgradeType.KnifeSplashRadiusPercent);
        Add("Aftershock +{0}", "Splash hits deal {0} more of the weapon's damage.", ValueFormat.Percent, UpgradeType.KnifeSplashDamagePercentFlat);
        Add("Greater Aftershock +{0}", "Increases current splash damage by {0}.", ValueFormat.Percent, UpgradeType.KnifeSplashDamagePercentPercent);
        Add("Echo Edge +{0}", "Hits gain {0} chance to repeat for half damage.", ValueFormat.Percent, UpgradeType.KnifeEchoStrikeChance);

        // Shooter only
        Add("Multishot +{0}", "Fires {0} additional projectiles with every attack.", ValueFormat.Int, UpgradeType.ShooterProjectileCount);
        Add("Steady Aim +{0}", "Reduces projectile spread by {0}, making shots more accurate.", ValueFormat.Degrees, UpgradeType.ShooterSpreadAngleFlat);
        Add("Deadeye +{0}", "Reduces current projectile spread by {0}.", ValueFormat.Percent, UpgradeType.ShooterSpreadAnglePercent);
        Add("Swift Projectiles +{0}", "Projectiles travel {0} faster.", ValueFormat.F1, UpgradeType.ShooterProjectileSpeedFlat);
        Add("Arcane Velocity +{0}", "Increases projectile speed by {0}.", ValueFormat.Percent, UpgradeType.ShooterProjectileSpeedPercent);
        Add("Extended Flight +{0}", "Projectiles remain active for {0} longer.", ValueFormat.Seconds1, UpgradeType.ShooterLifetimeFlat);
        Add("Unfading Shot +{0}", "Increases projectile lifetime by {0}.", ValueFormat.Percent, UpgradeType.ShooterLifetimePercent);
        Add("Chain Lightning +{0}", "Projectiles seek and strike {0} additional enemies.", ValueFormat.Int, UpgradeType.ShooterChainHits);
        Add("Forked Rounds +{0}", "Projectiles gain {0} chance to split into two weaker side shots.", ValueFormat.Percent, UpgradeType.ShooterForkShotChance);

        // Tick
        Add("Quickened Strikes -{0}", "Attacks trigger {0} sooner each cycle.", ValueFormat.Seconds2, UpgradeType.TickRateFlat);
        Add("Battle Rhythm +{0}", "Reduces the delay between attacks by {0}.", ValueFormat.Percent, UpgradeType.TickRatePercent);
        Add("Larger Volley +{0}", "Adds {0} attacks to every burst.", ValueFormat.Int, UpgradeType.BurstCountFlat);
        Add("Relentless Volley +{0}", "Increases attacks per burst by {0}.", ValueFormat.Percent, UpgradeType.BurstCountPercent);
        Add("Rapid Burst -{0}", "Each attack within a burst fires {0} sooner.", ValueFormat.Seconds2, UpgradeType.BurstSpacingFlat);
        Add("Flurry +{0}", "Reduces the delay between burst attacks by {0}.", ValueFormat.Percent, UpgradeType.BurstSpacingPercent);

        return table;
    }

    private const string NumColor = "#8888FF";
    private static string C(string s) => $"<color={NumColor}>{s}</color>";

    private string FormatValue(ValueFormat format) => format switch
    {
        ValueFormat.Int => C(Mathf.RoundToInt(value).ToString()),
        ValueFormat.Percent => C((value * 100f).ToString("F0")) + "%",
        ValueFormat.F1 => C(value.ToString("F1")),
        ValueFormat.F2 => C(value.ToString("F2")),
        ValueFormat.Seconds1 => C(value.ToString("F1")) + "s",
        ValueFormat.Seconds2 => C(value.ToString("F2")) + "s",
        ValueFormat.Multiplier => C(value.ToString("F2")) + "x",
        ValueFormat.Degrees => C(value.ToString("F1")) + "°",
        ValueFormat.DamageType => ClampToDamageType(Mathf.RoundToInt(value)).ToString(),
        ValueFormat.StatusType => ((StatusEffectSystem.StatusType)Mathf.Clamp(
            Mathf.RoundToInt(value), 0,
            System.Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length - 1)).ToString(),
        _ => string.Empty,
    };

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;

        if (upgradeTexts.TryGetValue(upgradeType, out var text))
        {
            string v = FormatValue(text.format);
            Upgrade.powerUpName = string.Format(text.title, v);
            Upgrade.powerUpDescription = string.Format(text.description, v);
        }
        else
        {
            Upgrade.powerUpName = "No Upgrade";
            Upgrade.powerUpDescription = "This upgrade slot is empty.";
        }

        // Append the owning weapon name.
        if (transform.parent != null && !string.IsNullOrEmpty(Upgrade.powerUpName))
            Upgrade.powerUpName = $"{transform.parent.name} - {Upgrade.powerUpName}";
    }

    private void TryAssignIconFromParent()
    {
        if (Upgrade == null || transform.parent == null) return;

        if (transform.parent.TryGetComponent(out Knife knife) && knife.weaponSprite != null)
        {
            Upgrade.powerUpIcon = knife.weaponSprite;
            return;
        }
        if (transform.parent.TryGetComponent(out SimpleShooter shooter) && shooter.weaponSprite != null)
        {
            Upgrade.powerUpIcon = shooter.weaponSprite;
        }
    }

    // ---------------------- Apply ----------------------

    public void ApplyUpgrade()
    {
        if (upgradeType == UpgradeType.None)
            return;

        var parent = transform.parent;
        if (parent == null) return;

        if (IsKnifeType(upgradeType) && parent.TryGetComponent(out Knife knife))
            ApplyKnifeUpgrade(knife);
        else if (IsShooterType(upgradeType) && parent.TryGetComponent(out SimpleShooter shooter))
            ApplyShooterUpgrade(shooter);
        else if (IsTickType(upgradeType) && parent.TryGetComponent(out WeaponTick tick))
            ApplyTickUpgrade(tick);
    }

    private void ApplyKnifeUpgrade(Knife knife)
    {
        switch (upgradeType)
        {
            case UpgradeType.KnifeDamageFlat:
                knife.minDamage += Mathf.RoundToInt(value);
                knife.damage += Mathf.RoundToInt(value);
                break;
            case UpgradeType.KnifeDamagePercent:
                knife.minDamage = Mathf.RoundToInt(knife.minDamage * (1f + value));
                knife.damage = Mathf.RoundToInt(knife.damage * (1f + value));
                break;
            case UpgradeType.KnifeRadiusFlat:
                knife.radius += value;
                break;
            case UpgradeType.KnifeRadiusPercent:
                knife.radius *= (1f + value);
                break;
            case UpgradeType.KnifeMaxTargetsFlat:
                knife.maxTargetsPerTick += Mathf.RoundToInt(value);
                break;
            case UpgradeType.KnifeLifestealFlat:
                knife.lifestealPercent += value;
                break;
            case UpgradeType.KnifeLifestealPercent:
                knife.lifestealPercent *= (1f + value);
                break;
            case UpgradeType.KnifeCritChanceFlat:
                knife.critChance += value;
                break;
            case UpgradeType.KnifeCritMultiplierFlat:
                knife.critMultiplier += value;
                break;
            case UpgradeType.KnifeSplashRadiusFlat:
                knife.splashRadius += value;
                break;
            case UpgradeType.KnifeSplashRadiusPercent:
                knife.splashRadius *= (1f + value);
                break;
            case UpgradeType.KnifeSplashDamagePercentFlat:
                knife.splashDamagePercent += value;
                break;
            case UpgradeType.KnifeSplashDamagePercentPercent:
                knife.splashDamagePercent *= (1f + value);
                break;
            case UpgradeType.KnifeStatusApplyChanceFlat:
                knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance + value);
                break;
            case UpgradeType.KnifeStatusApplyChancePercent:
                knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance * (1f + value));
                break;
            case UpgradeType.KnifeStatusDurationFlat:
                knife.statusEffectDuration += value;
                break;
            case UpgradeType.KnifeStatusDurationPercent:
                knife.statusEffectDuration *= (1f + value);
                break;
            case UpgradeType.KnifeEnableStatusEffect:
                knife.applyStatusEffectOnHit = true;
                break;
            case UpgradeType.KnifeStatusEffectIndex:
                knife.EnableOnHitEffectByIndex(Mathf.RoundToInt(value));
                break;
            case UpgradeType.KnifeDamageTypeIndex:
                knife.damageType = ResolveDifferentDamageType(knife.damageType, value);
                break;
            case UpgradeType.KnifeKnockbackFlat:
                knife.knockbackForce += value;
                break;
            case UpgradeType.KnifeCullThreshold:
                knife.cullThreshold = Mathf.Clamp01(knife.cullThreshold + value);
                break;
            case UpgradeType.KnifeEchoStrikeChance:
                knife.echoStrikeChance = Mathf.Clamp01(knife.echoStrikeChance + value);
                break;
        }
    }

    private void ApplyShooterUpgrade(SimpleShooter shooter)
    {
        switch (upgradeType)
        {
            case UpgradeType.ShooterDamageFlat:
                shooter.minDamage += Mathf.RoundToInt(value);
                shooter.damage += Mathf.RoundToInt(value);
                break;
            case UpgradeType.ShooterDamagePercent:
                shooter.minDamage = Mathf.RoundToInt(shooter.minDamage * (1f + value));
                shooter.damage = Mathf.RoundToInt(shooter.damage * (1f + value));
                break;
            case UpgradeType.ShooterProjectileCount:
                shooter.projectileCount += Mathf.RoundToInt(value);
                break;
            case UpgradeType.ShooterSpreadAngleFlat:
                shooter.spreadAngle += value;
                break;
            case UpgradeType.ShooterSpreadAnglePercent:
                shooter.spreadAngle *= (1f + value);
                break;
            case UpgradeType.ShooterProjectileSpeedFlat:
                shooter.shootForce += value;
                break;
            case UpgradeType.ShooterProjectileSpeedPercent:
                shooter.shootForce *= (1f + value);
                break;
            case UpgradeType.ShooterLifetimeFlat:
                shooter.bulletLifetime += value;
                break;
            case UpgradeType.ShooterLifetimePercent:
                shooter.bulletLifetime *= (1f + value);
                break;
            case UpgradeType.ShooterCritChanceFlat:
                shooter.critChance += value;
                break;
            case UpgradeType.ShooterCritMultiplierFlat:
                shooter.critMultiplier += value;
                break;
            case UpgradeType.ShooterStatusApplyChanceFlat:
                shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance + value);
                break;
            case UpgradeType.ShooterStatusApplyChancePercent:
                shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance * (1f + value));
                break;
            case UpgradeType.ShooterStatusDurationFlat:
                shooter.statusEffectDuration += value;
                break;
            case UpgradeType.ShooterStatusDurationPercent:
                shooter.statusEffectDuration *= (1f + value);
                break;
            case UpgradeType.ShooterEnableStatusEffect:
                shooter.applyStatusEffectOnHit = true;
                break;
            case UpgradeType.ShooterStatusEffectIndex:
                shooter.EnableOnHitEffectByIndex(Mathf.RoundToInt(value));
                break;
            case UpgradeType.ShooterDamageTypeIndex:
                shooter.damageType = ResolveDifferentDamageType(shooter.damageType, value);
                break;
            case UpgradeType.ShooterKnockbackFlat:
                shooter.knockbackForce += value;
                break;
            case UpgradeType.ShooterCullThreshold:
                shooter.cullThreshold = Mathf.Clamp01(shooter.cullThreshold + value);
                break;
            case UpgradeType.ShooterChainHits:
                shooter.chainHits += Mathf.Max(1, Mathf.RoundToInt(value));
                break;
            case UpgradeType.ShooterForkShotChance:
                shooter.forkShotChance = Mathf.Clamp01(shooter.forkShotChance + value);
                break;
        }
    }

    private void ApplyTickUpgrade(WeaponTick tick)
    {
        switch (upgradeType)
        {
            case UpgradeType.TickRateFlat:
                tick.interval = Mathf.Max(0.05f, tick.interval - value);
                break;
            case UpgradeType.TickRatePercent:
                tick.interval = Mathf.Max(0.05f, tick.interval * (1f - value));
                break;
            case UpgradeType.BurstCountFlat:
                tick.burstCount += Mathf.RoundToInt(value);
                break;
            case UpgradeType.BurstCountPercent:
                tick.burstCount = Mathf.RoundToInt(tick.burstCount * (1f + value));
                break;
            case UpgradeType.BurstSpacingFlat:
                tick.burstSpacing = Mathf.Max(0.01f, tick.burstSpacing - value);
                break;
            case UpgradeType.BurstSpacingPercent:
                tick.burstSpacing = Mathf.Max(0.01f, tick.burstSpacing * (1f - value));
                break;
        }
    }

    private static SimpleHealth.DamageType ClampToDamageType(int rawIndex)
    {
        // Ensure value maps into enum range 0..4
        return (SimpleHealth.DamageType)Mathf.Clamp(rawIndex, 0, 4);
    }

    private static SimpleHealth.DamageType ResolveDifferentDamageType(
        SimpleHealth.DamageType current,
        float rolledValue)
    {
        var rolled = ClampToDamageType(Mathf.RoundToInt(rolledValue));
        if (rolled != current) return rolled;

        int count = System.Enum.GetValues(typeof(SimpleHealth.DamageType)).Length;
        return (SimpleHealth.DamageType)(((int)current + 1) % count);
    }

    // ---------------------- Runtime generation ----------------------

    private List<UpgradeType> GetAllowedTypes()
    {
        var allowed = new List<UpgradeType>();
        foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType)))
        {
            if (!IsGeneratedType(t)) continue;
            if (IsTypeAllowedForParent(t)) allowed.Add(t);
        }

        var parent = transform.parent;
        bool hasOnHitEffect = parent != null &&
            ((parent.TryGetComponent(out Knife knife) && knife.applyStatusEffectOnHit) ||
             (parent.TryGetComponent(out SimpleShooter shooter) && shooter.applyStatusEffectOnHit));
        if (!hasOnHitEffect)
            allowed.RemoveAll(IsStatusType);

        allowed.RemoveAll(IsPercentTypeWithoutBaseValue);

        return allowed;
    }

    private bool IsPercentTypeWithoutBaseValue(UpgradeType type)
    {
        const float epsilon = 0.0001f;
        var parent = transform.parent;
        if (parent == null) return false;

        switch (type)
        {
            case UpgradeType.KnifeDamagePercent:
                return parent.TryGetComponent(out Knife knifeDamage) && knifeDamage.damage <= 0;
            case UpgradeType.KnifeRadiusPercent:
                return parent.TryGetComponent(out Knife knifeRadius) && knifeRadius.radius <= epsilon;
            case UpgradeType.KnifeLifestealPercent:
                return parent.TryGetComponent(out Knife knifeLifesteal) && knifeLifesteal.lifestealPercent <= epsilon;
            case UpgradeType.KnifeSplashRadiusPercent:
                return parent.TryGetComponent(out Knife knifeSplashRadius) && knifeSplashRadius.splashRadius <= epsilon;
            case UpgradeType.KnifeSplashDamagePercentPercent:
                return parent.TryGetComponent(out Knife knifeSplashDamage) && knifeSplashDamage.splashDamagePercent <= epsilon;
            case UpgradeType.KnifeStatusApplyChancePercent:
                return parent.TryGetComponent(out Knife knifeStatusChance) && knifeStatusChance.statusApplyChance <= epsilon;
            case UpgradeType.KnifeStatusDurationPercent:
                return parent.TryGetComponent(out Knife knifeStatusDuration) && knifeStatusDuration.statusEffectDuration <= epsilon;
            case UpgradeType.ShooterDamagePercent:
                return parent.TryGetComponent(out SimpleShooter shooterDamage) && shooterDamage.damage <= 0;
            case UpgradeType.ShooterSpreadAnglePercent:
                return parent.TryGetComponent(out SimpleShooter shooterSpread) && shooterSpread.spreadAngle <= epsilon;
            case UpgradeType.ShooterProjectileSpeedPercent:
                return parent.TryGetComponent(out SimpleShooter shooterSpeed) && shooterSpeed.shootForce <= epsilon;
            case UpgradeType.ShooterLifetimePercent:
                return parent.TryGetComponent(out SimpleShooter shooterLifetime) && shooterLifetime.bulletLifetime <= epsilon;
            case UpgradeType.ShooterStatusApplyChancePercent:
                return parent.TryGetComponent(out SimpleShooter shooterStatusChance) && shooterStatusChance.statusApplyChance <= epsilon;
            case UpgradeType.ShooterStatusDurationPercent:
                return parent.TryGetComponent(out SimpleShooter shooterStatusDuration) && shooterStatusDuration.statusEffectDuration <= epsilon;
            case UpgradeType.TickRatePercent:
                return parent.TryGetComponent(out WeaponTick tickRate) && tickRate.interval <= epsilon;
            case UpgradeType.BurstCountPercent:
                return parent.TryGetComponent(out WeaponTick burstCount) && burstCount.burstCount <= 0;
            case UpgradeType.BurstSpacingPercent:
                return parent.TryGetComponent(out WeaponTick burstSpacing) && burstSpacing.burstSpacing <= epsilon;
            default:
                return false;
        }
    }

    /// <summary>
    /// Configures this component as a freshly rolled upgrade offer for its parent
    /// weapon: picks a random allowed type (minus excludeTypes), rolls a value, and
    /// creates a new PowerUp entry whose powerUpObject is this GameObject, so that
    /// selecting it in the UI activates this object and applies the upgrade.
    /// Returns false when no upgrade type is available.
    /// </summary>
    public bool RandomizeAsOffer(ICollection<UpgradeType> excludeTypes = null)
    {
        var allowed = GetAllowedTypes();
        if (excludeTypes != null)
            allowed.RemoveAll(excludeTypes.Contains);
        if (allowed.Count == 0) return false;

        upgradeType = allowed[Random.Range(0, allowed.Count)];
        PowerUpRarity rarity = PowerUp.RollRandomRarity();
        value = ApplyRarityMultiplier(upgradeType, GetRandomValueForType(upgradeType), rarity);

        Upgrade = new PowerUp
        {
            powerUpObject = gameObject,
            IsWeapon = true,
            IsUpgrade = true,
            rarity = rarity
        };
        TryAssignIconFromParent();
        SetUpgradeInfo();
        return true;
    }

    private static float ApplyRarityMultiplier(UpgradeType type, float rolledValue, PowerUpRarity rarity)
    {
        if (!CanRarityMultiply(type))
            return rolledValue;

        return rolledValue * PowerUp.GetRarityMultiplier(rarity);
    }

    private static bool CanRarityMultiply(UpgradeType type)
    {
        return type != UpgradeType.KnifeDamageTypeIndex &&
               type != UpgradeType.ShooterDamageTypeIndex &&
               type != UpgradeType.KnifeStatusEffectIndex &&
               type != UpgradeType.ShooterStatusEffectIndex;
    }

    private static float RandomStatusEffectIndex() =>
        Mathf.Round(Random.Range(0f, (float)System.Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length - 1f));

    private float GetRandomValueForType(UpgradeType t)
    {
        if (GeneratedUpgradeSettings.TryRollWeapon(t, out float configuredValue))
            return configuredValue;

        switch (t)
        {
            // Shared Knife + Shooter ranges
            case UpgradeType.KnifeDamageFlat:
            case UpgradeType.ShooterDamageFlat:
                return Random.Range(1f, 6f);
            case UpgradeType.KnifeDamagePercent:
            case UpgradeType.ShooterDamagePercent:
                return Random.Range(0.03f, 0.12f);
            case UpgradeType.KnifeDamageTypeIndex:
            case UpgradeType.ShooterDamageTypeIndex:
                return RandomDamageTypeIndexDifferentFromParent();
            case UpgradeType.KnifeCritChanceFlat:
            case UpgradeType.ShooterCritChanceFlat:
                return Random.Range(0.02f, 0.10f);
            case UpgradeType.KnifeCritMultiplierFlat:
            case UpgradeType.ShooterCritMultiplierFlat:
                return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeStatusApplyChanceFlat:
            case UpgradeType.ShooterStatusApplyChanceFlat:
                return Random.Range(0.03f, 0.15f);
            case UpgradeType.KnifeStatusApplyChancePercent:
            case UpgradeType.ShooterStatusApplyChancePercent:
                return Random.Range(0.05f, 0.25f);
            case UpgradeType.KnifeStatusDurationFlat:
            case UpgradeType.ShooterStatusDurationFlat:
                return Random.Range(0.25f, 1.5f);
            case UpgradeType.KnifeStatusDurationPercent:
            case UpgradeType.ShooterStatusDurationPercent:
                return Random.Range(0.05f, 0.25f);
            case UpgradeType.KnifeEnableStatusEffect:
            case UpgradeType.ShooterEnableStatusEffect:
                return 0f; // toggle only
            case UpgradeType.KnifeStatusEffectIndex:
            case UpgradeType.ShooterStatusEffectIndex:
                return RandomStatusEffectIndex();
            case UpgradeType.KnifeKnockbackFlat:
            case UpgradeType.ShooterKnockbackFlat:
                return Random.Range(0.25f, 1.5f);
            case UpgradeType.KnifeCullThreshold:
            case UpgradeType.ShooterCullThreshold:
                return Random.Range(0.01f, 0.03f);
            case UpgradeType.ShooterChainHits:
                return 1f;
            case UpgradeType.KnifeEchoStrikeChance:
            case UpgradeType.ShooterForkShotChance:
                return Random.Range(0.08f, 0.20f);

            // Small integer counts (1..2)
            case UpgradeType.KnifeMaxTargetsFlat:
            case UpgradeType.ShooterProjectileCount:
            case UpgradeType.BurstCountFlat:
                return Mathf.Round(Random.Range(1f, 2.99f));

            // Knife
            case UpgradeType.KnifeRadiusFlat: return Random.Range(0.05f, 0.50f);
            case UpgradeType.KnifeRadiusPercent: return Random.Range(0.03f, 0.15f);
            case UpgradeType.KnifeLifestealFlat: return Random.Range(0.01f, 0.08f);
            case UpgradeType.KnifeLifestealPercent: return Random.Range(0.05f, 0.20f);
            case UpgradeType.KnifeSplashRadiusFlat: return Random.Range(0.10f, 0.75f);
            case UpgradeType.KnifeSplashRadiusPercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.KnifeSplashDamagePercentFlat: return Random.Range(0.03f, 0.15f);
            case UpgradeType.KnifeSplashDamagePercentPercent: return Random.Range(0.05f, 0.25f);

            // Shooter
            case UpgradeType.ShooterSpreadAngleFlat: return Random.Range(1f, 10f);
            case UpgradeType.ShooterSpreadAnglePercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.ShooterProjectileSpeedFlat: return Random.Range(0.25f, 2.5f);
            case UpgradeType.ShooterProjectileSpeedPercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.ShooterLifetimeFlat: return Random.Range(0.15f, 1f);
            case UpgradeType.ShooterLifetimePercent: return Random.Range(0.05f, 0.25f);

            // Tick
            case UpgradeType.TickRateFlat: return Random.Range(0.03f, 0.25f);
            case UpgradeType.TickRatePercent: return Random.Range(0.03f, 0.15f);
            case UpgradeType.BurstCountPercent: return Random.Range(0.05f, 0.25f);
            case UpgradeType.BurstSpacingFlat: return Random.Range(0.01f, 0.15f);
            case UpgradeType.BurstSpacingPercent: return Random.Range(0.05f, 0.25f);
        }
        return 0f;
    }

    private float RandomDamageTypeIndexDifferentFromParent()
    {
        var parent = transform.parent;
        SimpleHealth.DamageType current = SimpleHealth.DamageType.Physical;

        if (parent != null && parent.TryGetComponent(out Knife knife))
            current = knife.damageType;
        else if (parent != null && parent.TryGetComponent(out SimpleShooter shooter))
            current = shooter.damageType;

        int count = System.Enum.GetValues(typeof(SimpleHealth.DamageType)).Length;
        int rolled = Random.Range(0, count - 1);
        if (rolled >= (int)current) rolled++;
        return rolled;
    }
}
