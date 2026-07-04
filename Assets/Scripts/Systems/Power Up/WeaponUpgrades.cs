using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

// Handles a single weapon upgrade entry and (optionally) wires the *next* upgrade
// to the next sibling WeaponUpgrades component under the same parent.
// Also pushes that next upgrade's PowerUp into PowerUpChooser (expects a List<PowerUp>).
//
// Each UpgradeType's allowed parent, display text, applied effect and roll range are
// defined once in the Specs table below (see UpgradeSpec), instead of being spread
// across four separate switch statements that had to be kept in sync by hand.
public class WeaponUpgrades : MonoBehaviour
{
    public enum UpgradeType
    {
        None,

        // --- Knife ---
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

        // --- Shooter ---
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

        // --- WeaponTick ---
        TickRateFlat,
        TickRatePercent,
        BurstCountFlat,
        BurstCountPercent,
        BurstSpacingFlat,
        BurstSpacingPercent,

        Custom
    }

    private enum WeaponKind { Knife, Shooter, Tick }

    // One entry per UpgradeType: which parent it needs, how it reads (name/description),
    // what it does (Apply, operating through the adapters in WeaponUpgradeAdapters.cs),
    // and the range GenerateRandomUpgrade rolls from. Shared concepts (damage, crit,
    // on-hit status, damage type) reuse the exact same delegates for Knife and Shooter
    // instead of duplicating the logic per weapon type.
    private readonly struct UpgradeSpec
    {
        public readonly WeaponKind Kind;
        public readonly Func<float, string> NameFormat;
        public readonly Func<float, string> DescFormat;
        public readonly Action<object, float> Apply;
        public readonly float RollMin;
        public readonly float RollMax;
        public readonly bool RollIsInt;

        public UpgradeSpec(WeaponKind kind, Func<float, string> nameFormat, Func<float, string> descFormat,
            Action<object, float> apply, float rollMin, float rollMax, bool rollIsInt = false)
        {
            Kind = kind;
            NameFormat = nameFormat;
            DescFormat = descFormat;
            Apply = apply;
            RollMin = rollMin;
            RollMax = rollMax;
            RollIsInt = rollIsInt;
        }
    }

    private const string NumColor = "#8888FF";
    private static string C(string s) => $"<color={NumColor}>{s}</color>";
    private static string Flat(float v, string fmt = "F0") => C(v.ToString(fmt));
    private static string Pct(float v) => C((v * 100f).ToString("F0"));
    private static string IntFlat(float v) => C(Mathf.RoundToInt(v).ToString());

    private static SimpleHealth.DamageType ClampToDamageType(int rawIndex)
    {
        if (rawIndex < 0) rawIndex = 0;
        if (rawIndex > 4) rawIndex = 4;
        return (SimpleHealth.DamageType)rawIndex;
    }

    // ---- Shared concept implementations (used by both Knife and Shooter entries) ----

    private static string DamageFlatName(float v) => $"Damage Up +{IntFlat(v)}";
    private static string DamageFlatDesc(float v) => $"Increases weapon damage by {IntFlat(v)}.";
    private static void ApplyDamageFlat(object o, float v) { var t = (IWeaponStatTarget)o; t.Damage += Mathf.RoundToInt(v); }

    private static string DamagePercentName(float v) => $"Damage Up +{Pct(v)}%";
    private static string DamagePercentDesc(float v) => $"Increases weapon damage by {Pct(v)}%.";
    private static void ApplyDamagePercent(object o, float v) { var t = (IWeaponStatTarget)o; t.Damage = Mathf.RoundToInt(t.Damage * (1f + v)); }

    private static string DamageTypeName(float v) => $"Damage Type: {ClampToDamageType(Mathf.RoundToInt(v))}";
    private static string DamageTypeDesc(float v) => $"Changes this weapon's damage type to {ClampToDamageType(Mathf.RoundToInt(v))}.";
    private static void ApplyDamageType(object o, float v) { var t = (IWeaponStatTarget)o; t.DamageType = ClampToDamageType(Mathf.RoundToInt(v)); }

    private static string CritChanceName(float v) => $"Critical Chance +{Pct(v)}%";
    private static string CritChanceDesc(float v) => $"Raises chance to critically strike by {Pct(v)}%.";
    private static void ApplyCritChance(object o, float v) { var t = (IWeaponStatTarget)o; t.CritChance += v; }

    private static string CritMultName(float v) => $"Critical Damage +{Flat(v, "F2")}x";
    private static string CritMultDesc(float v) => $"Increases critical strike damage by {Flat(v, "F2")}x.";
    private static void ApplyCritMult(object o, float v) { var t = (IWeaponStatTarget)o; t.CritMultiplier += v; }

    private static string StatusChanceFlatName(float v) => $"Status Chance +{Pct(v)}%";
    private static string StatusChanceFlatDesc(float v) => $"Increases chance to inflict effects by {Pct(v)}%.";
    private static void ApplyStatusChanceFlat(object o, float v) { var t = (IWeaponStatTarget)o; t.StatusApplyChance += v; }

    private static string StatusChancePercentName(float v) => $"Status Chance +{Pct(v)}%";
    private static string StatusChancePercentDesc(float v) => $"Further improves effect application by {Pct(v)}%.";
    private static void ApplyStatusChancePercent(object o, float v) { var t = (IWeaponStatTarget)o; t.StatusApplyChance *= (1f + v); }

    private static string StatusDurationFlatName(float v) => $"Status Duration +{Flat(v, "F1")}s";
    private static string StatusDurationFlatDesc(float v) => $"Effects linger longer by {Flat(v, "F1")}s.";
    private static void ApplyStatusDurationFlat(object o, float v) { var t = (IWeaponStatTarget)o; t.StatusEffectDuration += v; }

    private static string StatusDurationPercentName(float v) => $"Status Duration +{Pct(v)}%";
    private static string StatusDurationPercentDesc(float v) => $"Effects linger longer by {Pct(v)}%.";
    private static void ApplyStatusDurationPercent(object o, float v) { var t = (IWeaponStatTarget)o; t.StatusEffectDuration *= (1f + v); }

    private static string EnableStatusName(float v) => "Enable Status On Hit";
    private static string EnableStatusDesc(float v) => "Enables inflicting status effects on hit.";
    private static void ApplyEnableStatus(object o, float v) { var t = (IWeaponStatTarget)o; t.ApplyStatusEffectOnHit = true; }

    private static string StatusIndexName(float v) => "Set Status Type";
    private static string StatusIndexDesc(float v) => $"Sets the status effect type (index {IntFlat(v)}).";
    private static void ApplyStatusIndex(object o, float v) { var t = (IWeaponStatTarget)o; t.EnableOnHitEffectByIndex(Mathf.RoundToInt(v)); }

    private static readonly int StatusTypeCount = Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length;

    private static readonly Dictionary<UpgradeType, UpgradeSpec> Specs = new()
    {
        // ------------- Knife -------------
        [UpgradeType.KnifeDamageFlat] = new(WeaponKind.Knife, DamageFlatName, DamageFlatDesc, ApplyDamageFlat, 2f, 12f),
        [UpgradeType.KnifeDamagePercent] = new(WeaponKind.Knife, DamagePercentName, DamagePercentDesc, ApplyDamagePercent, 0.05f, 0.25f),
        [UpgradeType.KnifeDamageTypeIndex] = new(WeaponKind.Knife, DamageTypeName, DamageTypeDesc, ApplyDamageType, 0f, 4f, rollIsInt: true),
        [UpgradeType.KnifeRadiusFlat] = new(WeaponKind.Knife,
            v => $"Range Up +{Flat(v, "F2")}", v => $"Increases attack reach by {Flat(v, "F2")}.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.Radius += v; }, 0.1f, 1.0f),
        [UpgradeType.KnifeRadiusPercent] = new(WeaponKind.Knife,
            v => $"Range Up +{Pct(v)}%", v => $"Increases attack reach by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.Radius *= (1f + v); }, 0.05f, 0.30f),
        [UpgradeType.KnifeMaxTargetsFlat] = new(WeaponKind.Knife,
            v => $"Additional Targets +{IntFlat(v)}", v => $"Allows strikes to affect {IntFlat(v)} additional target(s).",
            (o, v) => { var t = (IKnifeStatTarget)o; t.MaxTargetsPerTick += Mathf.RoundToInt(v); }, 1f, 3.99f, rollIsInt: true),
        [UpgradeType.KnifeLifestealFlat] = new(WeaponKind.Knife,
            v => $"Lifesteal Up +{Pct(v)}%", v => $"Increases lifesteal by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.LifestealPercent += v; }, 0.02f, 0.15f),
        [UpgradeType.KnifeLifestealPercent] = new(WeaponKind.Knife,
            v => $"Lifesteal Up +{Pct(v)}%", v => $"Further empowers lifesteal by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.LifestealPercent *= (1f + v); }, 0.10f, 0.40f),
        [UpgradeType.KnifeCritChanceFlat] = new(WeaponKind.Knife, CritChanceName, CritChanceDesc, ApplyCritChance, 0.03f, 0.20f),
        [UpgradeType.KnifeCritMultiplierFlat] = new(WeaponKind.Knife, CritMultName, CritMultDesc, ApplyCritMult, 0.10f, 0.60f),
        [UpgradeType.KnifeSplashRadiusFlat] = new(WeaponKind.Knife,
            v => $"Splash Radius +{Flat(v, "F2")}", v => $"Widens splash reach by {Flat(v, "F2")}.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.SplashRadius += v; }, 0.20f, 1.50f),
        [UpgradeType.KnifeSplashRadiusPercent] = new(WeaponKind.Knife,
            v => $"Splash Radius +{Pct(v)}%", v => $"Widens splash reach by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.SplashRadius *= (1f + v); }, 0.10f, 0.50f),
        [UpgradeType.KnifeSplashDamagePercentFlat] = new(WeaponKind.Knife,
            v => $"Splash Potency +{Pct(v)}%", v => $"Increases splash damage by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.SplashDamagePercent += v; }, 0.05f, 0.30f),
        [UpgradeType.KnifeSplashDamagePercentPercent] = new(WeaponKind.Knife,
            v => $"Splash Potency +{Pct(v)}%", v => $"Further empowers splash damage by {Pct(v)}%.",
            (o, v) => { var t = (IKnifeStatTarget)o; t.SplashDamagePercent *= (1f + v); }, 0.10f, 0.50f),
        [UpgradeType.KnifeStatusApplyChanceFlat] = new(WeaponKind.Knife, StatusChanceFlatName, StatusChanceFlatDesc, ApplyStatusChanceFlat, 0.05f, 0.30f),
        [UpgradeType.KnifeStatusApplyChancePercent] = new(WeaponKind.Knife, StatusChancePercentName, StatusChancePercentDesc, ApplyStatusChancePercent, 0.10f, 0.50f),
        [UpgradeType.KnifeStatusDurationFlat] = new(WeaponKind.Knife, StatusDurationFlatName, StatusDurationFlatDesc, ApplyStatusDurationFlat, 0.5f, 3.0f),
        [UpgradeType.KnifeStatusDurationPercent] = new(WeaponKind.Knife, StatusDurationPercentName, StatusDurationPercentDesc, ApplyStatusDurationPercent, 0.10f, 0.50f),
        [UpgradeType.KnifeEnableStatusEffect] = new(WeaponKind.Knife, EnableStatusName, EnableStatusDesc, ApplyEnableStatus, 0f, 0f),
        [UpgradeType.KnifeStatusEffectIndex] = new(WeaponKind.Knife, StatusIndexName, StatusIndexDesc, ApplyStatusIndex, 0f, StatusTypeCount - 1f, rollIsInt: true),

        // ------------- Shooter (shares concept delegates with Knife where identical) -------------
        [UpgradeType.ShooterDamageFlat] = new(WeaponKind.Shooter, DamageFlatName, DamageFlatDesc, ApplyDamageFlat, 2f, 12f),
        [UpgradeType.ShooterDamagePercent] = new(WeaponKind.Shooter, DamagePercentName, DamagePercentDesc, ApplyDamagePercent, 0.05f, 0.25f),
        [UpgradeType.ShooterDamageTypeIndex] = new(WeaponKind.Shooter, DamageTypeName, DamageTypeDesc, ApplyDamageType, 0f, 4f, rollIsInt: true),
        [UpgradeType.ShooterProjectileCount] = new(WeaponKind.Shooter,
            v => $"Projectile Count +{IntFlat(v)}", v => $"Fires {IntFlat(v)} additional projectile(s).",
            (o, v) => { var t = (IShooterStatTarget)o; t.ProjectileCount += Mathf.RoundToInt(v); }, 1f, 3.99f, rollIsInt: true),
        [UpgradeType.ShooterSpreadAngleFlat] = new(WeaponKind.Shooter,
            v => $"Spread +{Flat(v, "F1")}°", v => $"Narrows firing spread by {Flat(v, "F1")}°.",
            (o, v) => { var t = (IShooterStatTarget)o; t.SpreadAngle += v; }, 2f, 20f),
        [UpgradeType.ShooterSpreadAnglePercent] = new(WeaponKind.Shooter,
            v => $"Spread +{Pct(v)}%", v => $"Narrows firing spread by {Pct(v)}%.",
            (o, v) => { var t = (IShooterStatTarget)o; t.SpreadAngle *= (1f + v); }, 0.10f, 0.50f),
        [UpgradeType.ShooterProjectileSpeedFlat] = new(WeaponKind.Shooter,
            v => $"Projectile Speed +{Flat(v, "F1")}", v => $"Increases projectile speed by {Flat(v, "F1")}.",
            (o, v) => { var t = (IShooterStatTarget)o; t.ShootForce += v; }, 0.5f, 5f),
        [UpgradeType.ShooterProjectileSpeedPercent] = new(WeaponKind.Shooter,
            v => $"Projectile Speed +{Pct(v)}%", v => $"Increases projectile speed by {Pct(v)}%.",
            (o, v) => { var t = (IShooterStatTarget)o; t.ShootForce *= (1f + v); }, 0.10f, 0.50f),
        [UpgradeType.ShooterLifetimeFlat] = new(WeaponKind.Shooter,
            v => $"Projectile Lifetime +{Flat(v, "F1")}s", v => $"Extends projectile lifetime by {Flat(v, "F1")}s.",
            (o, v) => { var t = (IShooterStatTarget)o; t.BulletLifetime += v; }, 0.3f, 2f),
        [UpgradeType.ShooterLifetimePercent] = new(WeaponKind.Shooter,
            v => $"Projectile Lifetime +{Pct(v)}%", v => $"Extends projectile lifetime by {Pct(v)}%.",
            (o, v) => { var t = (IShooterStatTarget)o; t.BulletLifetime *= (1f + v); }, 0.10f, 0.50f),
        [UpgradeType.ShooterCritChanceFlat] = new(WeaponKind.Shooter, CritChanceName, CritChanceDesc, ApplyCritChance, 0.03f, 0.20f),
        [UpgradeType.ShooterCritMultiplierFlat] = new(WeaponKind.Shooter, CritMultName, CritMultDesc, ApplyCritMult, 0.10f, 0.60f),
        [UpgradeType.ShooterStatusApplyChanceFlat] = new(WeaponKind.Shooter, StatusChanceFlatName, StatusChanceFlatDesc, ApplyStatusChanceFlat, 0.05f, 0.30f),
        [UpgradeType.ShooterStatusApplyChancePercent] = new(WeaponKind.Shooter, StatusChancePercentName, StatusChancePercentDesc, ApplyStatusChancePercent, 0.10f, 0.50f),
        [UpgradeType.ShooterStatusDurationFlat] = new(WeaponKind.Shooter, StatusDurationFlatName, StatusDurationFlatDesc, ApplyStatusDurationFlat, 0.5f, 3.0f),
        [UpgradeType.ShooterStatusDurationPercent] = new(WeaponKind.Shooter, StatusDurationPercentName, StatusDurationPercentDesc, ApplyStatusDurationPercent, 0.10f, 0.50f),
        [UpgradeType.ShooterEnableStatusEffect] = new(WeaponKind.Shooter, EnableStatusName, EnableStatusDesc, ApplyEnableStatus, 0f, 0f),
        [UpgradeType.ShooterStatusEffectIndex] = new(WeaponKind.Shooter, StatusIndexName, StatusIndexDesc, ApplyStatusIndex, 0f, StatusTypeCount - 1f, rollIsInt: true),

        // ------------- Tick -------------
        [UpgradeType.TickRateFlat] = new(WeaponKind.Tick,
            v => $"Attack Interval −{Flat(v, "F2")}s", v => $"Reduces delay between attacks by {Flat(v, "F2")}s.",
            (o, v) => { var t = (ITickStatTarget)o; t.Interval = Mathf.Max(0.05f, t.Interval - v); }, 0.05f, 0.50f),
        [UpgradeType.TickRatePercent] = new(WeaponKind.Tick,
            v => $"Attack Interval −{Pct(v)}%", v => $"Reduces delay between attacks by {Pct(v)}%.",
            (o, v) => { var t = (ITickStatTarget)o; t.Interval = Mathf.Max(0.05f, t.Interval * (1f - v)); }, 0.05f, 0.30f),
        [UpgradeType.BurstCountFlat] = new(WeaponKind.Tick,
            v => $"Burst Count +{IntFlat(v)}", v => $"Increases shots per burst by {IntFlat(v)}.",
            (o, v) => { var t = (ITickStatTarget)o; t.BurstCount += Mathf.RoundToInt(v); }, 1f, 3.99f, rollIsInt: true),
        [UpgradeType.BurstCountPercent] = new(WeaponKind.Tick,
            v => $"Burst Count +{Pct(v)}%", v => $"Increases shots per burst by {Pct(v)}%.",
            (o, v) => { var t = (ITickStatTarget)o; t.BurstCount = Mathf.RoundToInt(t.BurstCount * (1f + v)); }, 0.10f, 0.50f),
        [UpgradeType.BurstSpacingFlat] = new(WeaponKind.Tick,
            v => $"Burst Delay −{Flat(v, "F2")}s", v => $"Reduces delay between burst shots by {Flat(v, "F2")}s.",
            (o, v) => { var t = (ITickStatTarget)o; t.BurstSpacing = Mathf.Max(0.01f, t.BurstSpacing - v); }, 0.02f, 0.30f),
        [UpgradeType.BurstSpacingPercent] = new(WeaponKind.Tick,
            v => $"Burst Delay −{Pct(v)}%", v => $"Reduces delay between burst shots by {Pct(v)}%.",
            (o, v) => { var t = (ITickStatTarget)o; t.BurstSpacing = Mathf.Max(0.01f, t.BurstSpacing * (1f - v)); }, 0.10f, 0.50f),
    };

    // Once the authored chain runs out (no next sibling), CreateGeneratedNextUpgrade()
    // synthesizes further links at runtime so leveling never dead-ends. Soft cap guards
    // against unbounded GameObject growth over a very long play session.
    private const int MaxGeneratedDepth = 200;

    private PowerUpChooser powerUpChooser;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with WeaponUpgrades in the same parent.")]
    public WeaponUpgrades nextUpgrade;

    [Tooltip("True only for links synthesized at runtime past the authored chain's end.")]
    public bool isGenerated;
    public int generatedDepth;

    // Icon now auto-inferred from parent weapon (Knife/SimpleShooter).

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;

    [Header("Custom Hooks")]
    [Tooltip("Invoked in Awake if UpgradeType.Custom is selected.")]
    public UnityEvent onCustomAwake;

    [Tooltip("Acts as integer for flat amounts (rounded), or as a percent when the upgrade type is % based (e.g., 0.25 = 25%).")]
    public float value = 0f;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        powerUpChooser = UpgradeChainUtil.GetChooser();

        AutoAssignNextUpgrade();      // make sure nextUpgrade is always the next sibling with WeaponUpgrades
        EnqueueNextUpgradeOnce();     // push next Upgrade asset into chooser (expects List<PowerUp>)

        // Ensure icon: inherit from parent weapon if available
        TryAssignIconFromParent();

        // Normalize type if not allowed for the current parent
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        SetUpgradeInfo();

        if (upgradeType == UpgradeType.Custom)
        {
            onCustomAwake?.Invoke();
        }

        ApplyUpgrade();
    }

    private void OnEnable()
    {
        // Editor recompile / domain reload safety
        AutoAssignNextUpgrade();
        EnqueueNextUpgradeOnce();
    }

    private void OnValidate()
    {
        // Keep next wired live in the editor
        AutoAssignNextUpgrade();

        // Keep name/description (and icon) up to date while editing
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        TryAssignIconFromParent();

        SetUpgradeInfo();
    }

    private void OnTransformParentChanged()
    {
        AutoAssignNextUpgrade();
    }

    private void OnTransformChildrenChanged()
    {
        AutoAssignNextUpgrade();
    }

    // ---------------------- Auto-wire NEXT ----------------------

    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = UpgradeChainUtil.FindNextSibling<WeaponUpgrades>(transform);

        // Authored chain exhausted: synthesize the next link so leveling doesn't dead-end.
        // Runtime-only — never touches the authored prefab/editor state.
        if (nextUpgrade == null && Application.isPlaying)
            nextUpgrade = CreateGeneratedNextUpgrade();

#if UNITY_EDITOR
        if (old != nextUpgrade)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Synthesizes one more upgrade as a new sibling under the same parent, reusing
    /// the exact same random-roll/name/description logic as the editor-only
    /// "Generate Random Upgrade" tool. Once parented, UpgradeChainUtil.FindNextSibling
    /// picks it up like any authored upgrade on future calls, so no extra bookkeeping
    /// is needed to avoid re-generating it.
    /// </summary>
    private WeaponUpgrades CreateGeneratedNextUpgrade()
    {
        if (transform.parent == null) return null;
        if (generatedDepth >= MaxGeneratedDepth) return null;

        var go = new GameObject($"{transform.parent.name} - Generated Upgrade");
        go.SetActive(false); // must precede AddComponent so Awake never fires prematurely
        go.transform.SetParent(transform.parent, false);

        var wu = go.AddComponent<WeaponUpgrades>();
        wu.isGenerated = true;
        wu.generatedDepth = generatedDepth + 1;
        wu.Upgrade = new PowerUp
        {
            IsWeapon = false,
            IsAccessory = false,
            weight = Upgrade != null ? Upgrade.weight : 1f,
            powerUpObject = go,
        };

        wu.TryAssignIconFromParent();
        wu.GenerateRandomUpgrade();

        if (wu.upgradeType == UpgradeType.None)
        {
            Destroy(go);
            return null;
        }

        return wu;
    }

    private void EnqueueNextUpgradeOnce()
    {
        if (nextUpgrade == null) return;
        UpgradeChainUtil.EnqueueOnce(powerUpChooser, nextUpgrade.Upgrade);
    }

    // ---------------------- Helpers ----------------------

    private bool HasParent<T>() where T : Component
    {
        if (transform.parent == null) return false;
        return transform.parent.GetComponent<T>() != null;
    }

    private bool IsTypeAllowedForParent(UpgradeType type)
    {
        // None/Custom always allowed
        if (type == UpgradeType.None || type == UpgradeType.Custom) return true;

        if (!Specs.TryGetValue(type, out var spec)) return false;

        return spec.Kind switch
        {
            WeaponKind.Knife => HasParent<Knife>(),
            WeaponKind.Shooter => HasParent<SimpleShooter>(),
            WeaponKind.Tick => HasParent<WeaponTick>(),
            _ => false,
        };
    }

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;

        // Custom leaves name/description alone
        if (upgradeType == UpgradeType.Custom) return;

        if (Specs.TryGetValue(upgradeType, out var spec))
        {
            Upgrade.powerUpName = spec.NameFormat(value);
            Upgrade.powerUpDescription = spec.DescFormat(value);
        }
        else
        {
            Upgrade.powerUpName = "No Upgrade";
            Upgrade.powerUpDescription = "This upgrade slot is empty.";
        }

        // Append parent name unless Custom (handled above) or no parent
        if (transform.parent != null && !string.IsNullOrEmpty(Upgrade.powerUpName))
        {
            Upgrade.powerUpName = $"{transform.parent.name} - {Upgrade.powerUpName}";
        }
    }

    private void TryAssignIconFromParent()
    {
        if (Upgrade == null) return;
        // Read weapon sprite from parent weapon behaviours
        if (transform.parent != null)
        {
            if (transform.parent.TryGetComponent(out Knife knife))
            {
                if (knife.weaponSprite != null)
                {
                    Upgrade.powerUpIcon = knife.weaponSprite;
                    return;
                }
            }
            if (transform.parent.TryGetComponent(out SimpleShooter shooter))
            {
                if (shooter.weaponSprite != null)
                {
                    Upgrade.powerUpIcon = shooter.weaponSprite;
                    return;
                }
            }
        }
    }

    public void ApplyUpgrade()
    {
        if (upgradeType == UpgradeType.Custom || upgradeType == UpgradeType.None)
            return;

        if (!Specs.TryGetValue(upgradeType, out var spec)) return;
        if (transform.parent == null) return;

        object adapter = spec.Kind switch
        {
            WeaponKind.Knife => transform.parent.TryGetComponent(out Knife knife) ? new KnifeUpgradeAdapter(knife) : null,
            WeaponKind.Shooter => transform.parent.TryGetComponent(out SimpleShooter shooter) ? new ShooterUpgradeAdapter(shooter) : null,
            WeaponKind.Tick => transform.parent.TryGetComponent(out WeaponTick tick) ? new TickUpgradeAdapter(tick) : null,
            _ => null,
        };
        if (adapter == null) return;

        spec.Apply(adapter, value);
    }

#if UNITY_EDITOR
    // ---------- Custom Inspector to Filter Enum + show read-only next ----------
    [UnityEditor.CustomEditor(typeof(WeaponUpgrades))]
    private class WeaponUpgradesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var wu = (WeaponUpgrades)target;
            var so = serializedObject;

            so.Update();

            // Draw PowerUp first
            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("Upgrade"));

            // Read-only nextUpgrade display with a refresh button
            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.ObjectField("Next Upgrade (auto)", wu.nextUpgrade, typeof(WeaponUpgrades), true);
            }
            if (UnityEngine.GUILayout.Button("Refresh Next"))
            {
                wu.AutoAssignNextUpgrade();
                UnityEditor.EditorUtility.SetDirty(wu);
            }

            // Filter choices based on parent components
            var all = (WeaponUpgrades.UpgradeType[])System.Enum.GetValues(typeof(WeaponUpgrades.UpgradeType));
            System.Collections.Generic.List<WeaponUpgrades.UpgradeType> allowed = new();

            foreach (var t in all)
            {
                if (wu.IsTypeAllowedForParent(t))
                    allowed.Add(t);
            }
            // After: value/other fields + so.ApplyModifiedProperties() if you prefer
            // Find the serialized property for the UnityEvent
            var customEventProp = so.FindProperty("onCustomAwake");

            // Only show the event hook when the selected type is Custom
            if (wu.upgradeType == WeaponUpgrades.UpgradeType.Custom)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Custom Events", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.PropertyField(customEventProp);
            }

            if (allowed.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox("No valid upgrades for this parent. Add Knife, SimpleShooter, or WeaponTick to the parent.", UnityEditor.MessageType.Warning);
            }

            // Current selection index within allowed list
            int currentIndex = Mathf.Max(0, allowed.IndexOf(wu.upgradeType));
            if (currentIndex < 0) currentIndex = 0;

            // Display names
            string[] names = new string[allowed.Count];
            for (int i = 0; i < allowed.Count; i++)
                names[i] = allowed[i].ToString();

            // Draw popup
            int newIndex = (allowed.Count > 0)
                ? UnityEditor.EditorGUILayout.Popup("Upgrade Type", currentIndex, names)
                : 0;

            // Apply selection
            if (allowed.Count > 0)
            {
                var newType = allowed[newIndex];
                if (newType != wu.upgradeType)
                {
                    UnityEditor.Undo.RecordObject(wu, "Change Upgrade Type");
                    wu.upgradeType = newType;
                    wu.SetUpgradeInfo();
                    UnityEditor.EditorUtility.SetDirty(wu);
                }
            }

            // Value field
            var valueProp = so.FindProperty("value");
            UnityEditor.EditorGUILayout.PropertyField(valueProp);

            // Warn if invalid (shouldn’t happen thanks to filtering, but still safe)
            if (!wu.IsTypeAllowedForParent(wu.upgradeType))
            {
                UnityEditor.EditorGUILayout.HelpBox("Selected UpgradeType is not valid for this parent. It will be treated as 'None'.", UnityEditor.MessageType.Warning);
            }

            so.ApplyModifiedProperties();

            // Show live preview of title/description if possible
            if (wu.Upgrade != null)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Preview", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.LabelField("Title", wu.Upgrade.powerUpName);
                UnityEditor.EditorGUILayout.LabelField("Description", wu.Upgrade.powerUpDescription, UnityEditor.EditorStyles.wordWrappedLabel);
            }
        }
    }
#endif

    // ---------------------- Tools ----------------------
    [ContextMenu("Generate Random Upgrade")]
    private void GenerateRandomUpgrade()
    {
        // Build allowed list excluding None/Custom
        var all = (UpgradeType[])System.Enum.GetValues(typeof(UpgradeType));
        System.Collections.Generic.List<UpgradeType> allowed = new();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == UpgradeType.None || t == UpgradeType.Custom) continue;
            if (IsTypeAllowedForParent(t)) allowed.Add(t);
        }
        if (allowed.Count == 0)
        {
            Debug.LogWarning("[WeaponUpgrades] No valid upgrade types for this parent.");
            return;
        }

        // Pick a type and value
        int pick = Random.Range(0, allowed.Count);
        var chosen = allowed[pick];

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Generate Random Upgrade");
#endif
        upgradeType = chosen;
        value = GetRandomValueForType(chosen);
        SetUpgradeInfo();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private float GetRandomValueForType(UpgradeType t)
    {
        if (!Specs.TryGetValue(t, out var spec)) return 0f;
        return spec.RollIsInt ? Mathf.Round(Random.Range(spec.RollMin, spec.RollMax)) : Random.Range(spec.RollMin, spec.RollMax);
    }
}
