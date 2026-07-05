using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
//
// Handles a single weapon upgrade entry and (optionally) wires the *next* upgrade
// to the next sibling WeaponUpgrades component under the same parent.
// Also pushes that next upgrade's PowerUp into PowerUpChooser (expects a List<PowerUp>).
//
public class WeaponUpgrades : MonoBehaviour
{
    // Serialized by value into scenes/prefabs — do NOT reorder or remove members.
    // Members are grouped in contiguous ranges per weapon (see IsTypeAllowedForParent).
    public enum UpgradeType
    {
        None,

        // --- Knife (KnifeDamageFlat..KnifeStatusEffectIndex) ---
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

        // --- Shooter (ShooterDamageFlat..ShooterStatusEffectIndex) ---
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

        // --- WeaponTick (TickRateFlat..BurstSpacingPercent) ---
        TickRateFlat,
        TickRatePercent,
        BurstCountFlat,
        BurstCountPercent,
        BurstSpacingFlat,
        BurstSpacingPercent,

        Custom
    }

    private PowerUpChooser powerUpChooser;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with WeaponUpgrades in the same parent.")]
    public WeaponUpgrades nextUpgrade;

    // Icon auto-inferred from parent weapon (Knife/SimpleShooter).

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
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        AutoAssignNextUpgrade();      // make sure nextUpgrade is always the next sibling with WeaponUpgrades
        EnqueueNextUpgradeOnce();     // push next Upgrade asset into chooser (expects List<PowerUp>)

        TryAssignIconFromParent();

        // Normalize type if not allowed for the current parent
        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        SetUpgradeInfo();

        if (upgradeType == UpgradeType.Custom)
            onCustomAwake?.Invoke();

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

        if (!IsTypeAllowedForParent(upgradeType))
            upgradeType = UpgradeType.None;

        TryAssignIconFromParent();
        SetUpgradeInfo();
    }

    private void OnTransformParentChanged() => AutoAssignNextUpgrade();

    private void OnTransformChildrenChanged() => AutoAssignNextUpgrade();

    // ---------------------- Auto-wire NEXT ----------------------

    /// <summary>
    /// Automatically sets nextUpgrade to the next sibling under the same parent
    /// that has a WeaponUpgrades component. If immediate next child doesn't have it,
    /// scans forward until it finds one. Clears if none found.
    /// </summary>
    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = null;

        var parent = transform.parent;
        if (parent == null) return;

        for (int i = transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).TryGetComponent(out WeaponUpgrades candidate))
            {
                nextUpgrade = candidate;
                break;
            }
        }

#if UNITY_EDITOR
        if (old != nextUpgrade)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// Safely avoids duplicates and nulls.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (powerUpChooser == null) return;
        if (nextUpgrade == null || nextUpgrade.Upgrade == null) return;

        var list = powerUpChooser.powerUps;
        if (list != null && !list.Contains(nextUpgrade.Upgrade))
            list.Add(nextUpgrade.Upgrade);
    }

    // ---------------------- Helpers ----------------------

    private bool HasParent<T>() where T : Component =>
        transform.parent != null && transform.parent.TryGetComponent<T>(out _);

    private static bool InRange(UpgradeType t, UpgradeType first, UpgradeType last) =>
        t >= first && t <= last;

    private static bool IsKnifeType(UpgradeType t) => InRange(t, UpgradeType.KnifeDamageFlat, UpgradeType.KnifeStatusEffectIndex);
    private static bool IsShooterType(UpgradeType t) => InRange(t, UpgradeType.ShooterDamageFlat, UpgradeType.ShooterStatusEffectIndex);
    private static bool IsTickType(UpgradeType t) => InRange(t, UpgradeType.TickRateFlat, UpgradeType.BurstSpacingPercent);

    private bool IsTypeAllowedForParent(UpgradeType type)
    {
        // None/Custom always allowed
        if (type == UpgradeType.None || type == UpgradeType.Custom) return true;
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
        DamageType  // enum name, uncolored
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
        Add("Damage Up +{0}", "Increases weapon damage by {0}.", ValueFormat.Int,
            UpgradeType.KnifeDamageFlat, UpgradeType.ShooterDamageFlat);
        Add("Damage Up +{0}", "Increases weapon damage by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeDamagePercent, UpgradeType.ShooterDamagePercent);
        Add("Damage Type: {0}", "Changes this weapon's damage type to {0}.", ValueFormat.DamageType,
            UpgradeType.KnifeDamageTypeIndex, UpgradeType.ShooterDamageTypeIndex);
        Add("Critical Chance +{0}", "Raises chance to critically strike by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeCritChanceFlat, UpgradeType.ShooterCritChanceFlat);
        Add("Critical Damage +{0}", "Increases critical strike damage by {0}.", ValueFormat.Multiplier,
            UpgradeType.KnifeCritMultiplierFlat, UpgradeType.ShooterCritMultiplierFlat);
        Add("Status Chance +{0}", "Increases chance to inflict effects by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeStatusApplyChanceFlat, UpgradeType.ShooterStatusApplyChanceFlat);
        Add("Status Chance +{0}", "Further improves effect application by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeStatusApplyChancePercent, UpgradeType.ShooterStatusApplyChancePercent);
        Add("Status Duration +{0}", "Effects linger longer by {0}.", ValueFormat.Seconds1,
            UpgradeType.KnifeStatusDurationFlat, UpgradeType.ShooterStatusDurationFlat);
        Add("Status Duration +{0}", "Effects linger longer by {0}.", ValueFormat.Percent,
            UpgradeType.KnifeStatusDurationPercent, UpgradeType.ShooterStatusDurationPercent);
        Add("Enable Status On Hit", "Enables inflicting status effects on hit.", ValueFormat.None,
            UpgradeType.KnifeEnableStatusEffect, UpgradeType.ShooterEnableStatusEffect);
        Add("Set Status Type", "Sets the status effect type (index {0}).", ValueFormat.Int,
            UpgradeType.KnifeStatusEffectIndex, UpgradeType.ShooterStatusEffectIndex);

        // Knife only
        Add("Range Up +{0}", "Increases attack reach by {0}.", ValueFormat.F2, UpgradeType.KnifeRadiusFlat);
        Add("Range Up +{0}", "Increases attack reach by {0}.", ValueFormat.Percent, UpgradeType.KnifeRadiusPercent);
        Add("Additional Targets +{0}", "Allows strikes to affect {0} additional target(s).", ValueFormat.Int, UpgradeType.KnifeMaxTargetsFlat);
        Add("Lifesteal Up +{0}", "Increases lifesteal by {0}.", ValueFormat.Percent, UpgradeType.KnifeLifestealFlat);
        Add("Lifesteal Up +{0}", "Further empowers lifesteal by {0}.", ValueFormat.Percent, UpgradeType.KnifeLifestealPercent);
        Add("Splash Radius +{0}", "Widens splash reach by {0}.", ValueFormat.F2, UpgradeType.KnifeSplashRadiusFlat);
        Add("Splash Radius +{0}", "Widens splash reach by {0}.", ValueFormat.Percent, UpgradeType.KnifeSplashRadiusPercent);
        Add("Splash Potency +{0}", "Increases splash damage by {0}.", ValueFormat.Percent, UpgradeType.KnifeSplashDamagePercentFlat);
        Add("Splash Potency +{0}", "Further empowers splash damage by {0}.", ValueFormat.Percent, UpgradeType.KnifeSplashDamagePercentPercent);

        // Shooter only
        Add("Projectile Count +{0}", "Fires {0} additional projectile(s).", ValueFormat.Int, UpgradeType.ShooterProjectileCount);
        Add("Spread +{0}", "Narrows firing spread by {0}.", ValueFormat.Degrees, UpgradeType.ShooterSpreadAngleFlat);
        Add("Spread +{0}", "Narrows firing spread by {0}.", ValueFormat.Percent, UpgradeType.ShooterSpreadAnglePercent);
        Add("Projectile Speed +{0}", "Increases projectile speed by {0}.", ValueFormat.F1, UpgradeType.ShooterProjectileSpeedFlat);
        Add("Projectile Speed +{0}", "Increases projectile speed by {0}.", ValueFormat.Percent, UpgradeType.ShooterProjectileSpeedPercent);
        Add("Projectile Lifetime +{0}", "Extends projectile lifetime by {0}.", ValueFormat.Seconds1, UpgradeType.ShooterLifetimeFlat);
        Add("Projectile Lifetime +{0}", "Extends projectile lifetime by {0}.", ValueFormat.Percent, UpgradeType.ShooterLifetimePercent);

        // Tick
        Add("Attack Interval −{0}", "Reduces delay between attacks by {0}.", ValueFormat.Seconds2, UpgradeType.TickRateFlat);
        Add("Attack Interval −{0}", "Reduces delay between attacks by {0}.", ValueFormat.Percent, UpgradeType.TickRatePercent);
        Add("Burst Count +{0}", "Increases shots per burst by {0}.", ValueFormat.Int, UpgradeType.BurstCountFlat);
        Add("Burst Count +{0}", "Increases shots per burst by {0}.", ValueFormat.Percent, UpgradeType.BurstCountPercent);
        Add("Burst Delay −{0}", "Reduces delay between burst shots by {0}.", ValueFormat.Seconds2, UpgradeType.BurstSpacingFlat);
        Add("Burst Delay −{0}", "Reduces delay between burst shots by {0}.", ValueFormat.Percent, UpgradeType.BurstSpacingPercent);

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
        _ => string.Empty,
    };

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;

        // Custom leaves name/description alone
        if (upgradeType == UpgradeType.Custom) return;

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

        // Append parent name unless Custom (handled above) or no parent
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
        // CUSTOM/None: intentionally do nothing
        if (upgradeType == UpgradeType.Custom || upgradeType == UpgradeType.None)
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
                knife.damage += Mathf.RoundToInt(value);
                break;
            case UpgradeType.KnifeDamagePercent:
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
                knife.damageType = ClampToDamageType(Mathf.RoundToInt(value));
                break;
        }
    }

    private void ApplyShooterUpgrade(SimpleShooter shooter)
    {
        switch (upgradeType)
        {
            case UpgradeType.ShooterDamageFlat:
                shooter.damage += Mathf.RoundToInt(value);
                break;
            case UpgradeType.ShooterDamagePercent:
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
                shooter.damageType = ClampToDamageType(Mathf.RoundToInt(value));
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
            var allowed = wu.GetAllowedTypes(includeNoneAndCustom: true);

            // Only show the event hook when the selected type is Custom
            if (wu.upgradeType == UpgradeType.Custom)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Custom Events", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("onCustomAwake"));
            }

            if (allowed.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox("No valid upgrades for this parent. Add Knife, SimpleShooter, or WeaponTick to the parent.", UnityEditor.MessageType.Warning);
            }
            else
            {
                int currentIndex = Mathf.Max(0, allowed.IndexOf(wu.upgradeType));

                string[] names = new string[allowed.Count];
                for (int i = 0; i < allowed.Count; i++)
                    names[i] = allowed[i].ToString();

                int newIndex = UnityEditor.EditorGUILayout.Popup("Upgrade Type", currentIndex, names);
                var newType = allowed[newIndex];
                if (newType != wu.upgradeType)
                {
                    UnityEditor.Undo.RecordObject(wu, "Change Upgrade Type");
                    wu.upgradeType = newType;
                    wu.SetUpgradeInfo();
                    UnityEditor.EditorUtility.SetDirty(wu);
                }
            }

            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("value"));

            // Warn if invalid (shouldn't happen thanks to filtering, but still safe)
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

    private List<UpgradeType> GetAllowedTypes(bool includeNoneAndCustom)
    {
        var allowed = new List<UpgradeType>();
        foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType)))
        {
            if (!includeNoneAndCustom && (t == UpgradeType.None || t == UpgradeType.Custom)) continue;
            if (IsTypeAllowedForParent(t)) allowed.Add(t);
        }
        return allowed;
    }

    [ContextMenu("Generate Random Upgrade")]
    private void GenerateRandomUpgrade()
    {
        var allowed = GetAllowedTypes(includeNoneAndCustom: false);
        if (allowed.Count == 0)
        {
            Debug.LogWarning("[WeaponUpgrades] No valid upgrade types for this parent.");
            return;
        }

        var chosen = allowed[Random.Range(0, allowed.Count)];

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

    private static float RandomStatusEffectIndex() =>
        Mathf.Round(Random.Range(0f, (float)System.Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length - 1f));

    private float GetRandomValueForType(UpgradeType t)
    {
        switch (t)
        {
            // Shared Knife + Shooter ranges
            case UpgradeType.KnifeDamageFlat:
            case UpgradeType.ShooterDamageFlat:
                return Random.Range(2f, 12f);
            case UpgradeType.KnifeDamagePercent:
            case UpgradeType.ShooterDamagePercent:
                return Random.Range(0.05f, 0.25f);
            case UpgradeType.KnifeCritChanceFlat:
            case UpgradeType.ShooterCritChanceFlat:
                return Random.Range(0.03f, 0.20f);
            case UpgradeType.KnifeCritMultiplierFlat:
            case UpgradeType.ShooterCritMultiplierFlat:
                return Random.Range(0.10f, 0.60f);
            case UpgradeType.KnifeStatusApplyChanceFlat:
            case UpgradeType.ShooterStatusApplyChanceFlat:
                return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeStatusApplyChancePercent:
            case UpgradeType.ShooterStatusApplyChancePercent:
                return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeStatusDurationFlat:
            case UpgradeType.ShooterStatusDurationFlat:
                return Random.Range(0.5f, 3.0f);
            case UpgradeType.KnifeStatusDurationPercent:
            case UpgradeType.ShooterStatusDurationPercent:
                return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeEnableStatusEffect:
            case UpgradeType.ShooterEnableStatusEffect:
                return 0f; // toggle only
            case UpgradeType.KnifeStatusEffectIndex:
            case UpgradeType.ShooterStatusEffectIndex:
                return RandomStatusEffectIndex();

            // Small integer counts (1..3)
            case UpgradeType.KnifeMaxTargetsFlat:
            case UpgradeType.ShooterProjectileCount:
            case UpgradeType.BurstCountFlat:
                return Mathf.Round(Random.Range(1f, 3.99f));

            // Knife
            case UpgradeType.KnifeRadiusFlat: return Random.Range(0.1f, 1.0f);
            case UpgradeType.KnifeRadiusPercent: return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeLifestealFlat: return Random.Range(0.02f, 0.15f);
            case UpgradeType.KnifeLifestealPercent: return Random.Range(0.10f, 0.40f);
            case UpgradeType.KnifeSplashRadiusFlat: return Random.Range(0.20f, 1.50f);
            case UpgradeType.KnifeSplashRadiusPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.KnifeSplashDamagePercentFlat: return Random.Range(0.05f, 0.30f);
            case UpgradeType.KnifeSplashDamagePercentPercent: return Random.Range(0.10f, 0.50f);

            // Shooter
            case UpgradeType.ShooterSpreadAngleFlat: return Random.Range(2f, 20f);
            case UpgradeType.ShooterSpreadAnglePercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterProjectileSpeedFlat: return Random.Range(0.5f, 5f);
            case UpgradeType.ShooterProjectileSpeedPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.ShooterLifetimeFlat: return Random.Range(0.3f, 2f);
            case UpgradeType.ShooterLifetimePercent: return Random.Range(0.10f, 0.50f);

            // Tick
            case UpgradeType.TickRateFlat: return Random.Range(0.05f, 0.50f);
            case UpgradeType.TickRatePercent: return Random.Range(0.05f, 0.30f);
            case UpgradeType.BurstCountPercent: return Random.Range(0.10f, 0.50f);
            case UpgradeType.BurstSpacingFlat: return Random.Range(0.02f, 0.30f);
            case UpgradeType.BurstSpacingPercent: return Random.Range(0.10f, 0.50f);
        }
        return 0f;
    }
}
