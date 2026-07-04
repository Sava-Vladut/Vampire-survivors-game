using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

// Handles a single accessory upgrade entry and wires the *next* upgrade to the next
// sibling AccessoriesUpgrades component under the same parent, mirroring WeaponUpgrades.
//
// Each UpgradeType's display text, applied effect and roll range are defined once in
// the Specs table below, instead of each authored step hand-wiring its own onAwake
// UnityEvent call into SimpleHealth/Snappy2DController. Effects apply to those two
// singleton components directly (via UpgradeChainUtil's cached lookups) rather than a
// transform.parent-scoped weapon, since accessories aren't attached to any one of them.
public class AccessoriesUpgrades : MonoBehaviour
{
    public enum UpgradeType
    {
        None,
        MaxHealthFlat,
        ArmorFlat,
        FireResistFlat,
        ColdResistFlat,
        LightningResistFlat,
        PoisonResistFlat,
        MoveSpeedFlat,
        DashSpeedFlat,
        DashCooldownFlat,
        Custom
    }

    // StatLine is the terse "+15 Armor" form used by Accessory.AccesoryDescription (and
    // parsed by Accessory.CombineStatLines for the aggregated stats panel). NameSuffix/
    // DescFormat feed the level-up choice UI, mirroring WeaponUpgrades' Specs table.
    private readonly struct UpgradeSpec
    {
        public readonly Func<float, string> StatLine;
        public readonly Func<float, string> NameSuffix;
        public readonly Func<float, string> DescFormat;
        public readonly Action<float> Apply;
        public readonly float RollMin;
        public readonly float RollMax;
        public readonly bool RollIsInt;

        public UpgradeSpec(Func<float, string> statLine, Func<float, string> nameSuffix, Func<float, string> descFormat,
            Action<float> apply, float rollMin, float rollMax, bool rollIsInt = false)
        {
            StatLine = statLine;
            NameSuffix = nameSuffix;
            DescFormat = descFormat;
            Apply = apply;
            RollMin = rollMin;
            RollMax = rollMax;
            RollIsInt = rollIsInt;
        }
    }

    private const string NumColor = "#8888FF";
    private static string C(string s) => $"<color={NumColor}>{s}</color>";
    private static string Flat(float v, string fmt = "F1") => C(v.ToString(fmt));
    private static string Pct(float v) => C((v * 100f).ToString("0.#"));
    private static string IntFlat(float v) => C(Mathf.RoundToInt(v).ToString());

    // Terse, unstyled number for the stat-line form ("+15 Armor", "-0.1 Dash Cooldown").
    private static string FmtNum(float v) =>
        Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

    private static readonly Dictionary<UpgradeType, UpgradeSpec> Specs = new()
    {
        [UpgradeType.MaxHealthFlat] = new(
            v => $"+{FmtNum(v)} Max Health",
            v => $"Max Health +{IntFlat(v)}",
            v => $"Increases max health by {IntFlat(v)}.",
            v => UpgradeChainUtil.GetHealth()?.IncreaseMaxHealth(Mathf.RoundToInt(v)),
            5f, 10f, rollIsInt: true),

        [UpgradeType.ArmorFlat] = new(
            v => $"+{FmtNum(v)} Armor",
            v => $"Armor +{IntFlat(v)}",
            v => $"Increases armor by {IntFlat(v)}.",
            v => UpgradeChainUtil.GetHealth()?.GiveArmor(v),
            10f, 15f),

        [UpgradeType.FireResistFlat] = new(
            v => $"+{FmtNum(v * 100f)}% Fire Resist",
            v => $"Fire Resist +{Pct(v)}%",
            v => $"Increases fire resistance by {Pct(v)}%.",
            v => UpgradeChainUtil.GetHealth()?.AddFireResist(v),
            0.05f, 0.10f),

        [UpgradeType.ColdResistFlat] = new(
            v => $"+{FmtNum(v * 100f)}% Cold Resist",
            v => $"Cold Resist +{Pct(v)}%",
            v => $"Increases cold resistance by {Pct(v)}%.",
            v => UpgradeChainUtil.GetHealth()?.AddColdResist(v),
            0.05f, 0.10f),

        [UpgradeType.LightningResistFlat] = new(
            v => $"+{FmtNum(v * 100f)}% Lightning Resist",
            v => $"Lightning Resist +{Pct(v)}%",
            v => $"Increases lightning resistance by {Pct(v)}%.",
            v => UpgradeChainUtil.GetHealth()?.AddLightningResist(v),
            0.05f, 0.10f),

        [UpgradeType.PoisonResistFlat] = new(
            v => $"+{FmtNum(v * 100f)}% Poison Resist",
            v => $"Poison Resist +{Pct(v)}%",
            v => $"Increases poison resistance by {Pct(v)}%.",
            v => UpgradeChainUtil.GetHealth()?.AddPoisonResist(v),
            0.05f, 0.10f),

        [UpgradeType.MoveSpeedFlat] = new(
            v => $"+{FmtNum(v)} Move Speed",
            v => $"Move Speed +{Flat(v)}",
            v => $"Increases move speed by {Flat(v)}.",
            v => UpgradeChainUtil.GetController()?.IncreaseMoveSpeed(v),
            0.1f, 0.3f),

        [UpgradeType.DashSpeedFlat] = new(
            v => $"+{FmtNum(v)} Dash Speed",
            v => $"Dash Speed +{Flat(v)}",
            v => $"Increases dash speed by {Flat(v)}.",
            v => UpgradeChainUtil.GetController()?.IncreaseDashSpeed(v),
            1.5f, 2.5f),

        [UpgradeType.DashCooldownFlat] = new(
            v => $"{FmtNum(v)} Dash Cooldown",
            v => $"Dash Cooldown {Flat(v, "F2")}s",
            v => $"Reduces dash cooldown by {Flat(-v, "F2")}s.",
            v => UpgradeChainUtil.GetController()?.IncreaseDashCooldown(v),
            -0.15f, -0.05f),
    };

    // Mirrors WeaponUpgrades' guard against unbounded GameObject growth over a long session.
    private const int MaxGeneratedDepth = 200;

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with AccessoriesUpgrades in the same parent.")]
    public AccessoriesUpgrades nextUpgrade;

    [Header("Upgrade Settings")]
    public UpgradeType upgradeType = UpgradeType.None;
    public float value;

    [Header("Custom Hooks")]
    [Tooltip("Invoked in Awake if UpgradeType.Custom is selected.")]
    public UnityEvent onCustomAwake;

    [Tooltip("True only for links synthesized at runtime past the authored chain's end.")]
    public bool isGenerated;
    public int generatedDepth;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        AutoAssignNextUpgrade();   // keep next wired
        EnqueueNextUpgradeOnce();  // push next's Upgrade into chooser once

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
        // Auto-set icon from parent's Accessory if available
        if (transform.parent != null && transform.parent.TryGetComponent(out Accessory accessory))
        {
            if (accessory.icon != null && Upgrade != null)
            {
                Upgrade.powerUpIcon = accessory.icon;
            }
        }

        // Keep next wired live in the editor as you reorder children
        AutoAssignNextUpgrade();

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

    /// <summary>
    /// Automatically sets nextUpgrade to the next sibling under the same parent
    /// that has an AccessoriesUpgrades component. If the immediate next child
    /// doesn't have it, scans forward until it finds one. Clears if none found.
    /// </summary>
    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = UpgradeChainUtil.FindNextSibling<AccessoriesUpgrades>(transform);

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
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (nextUpgrade == null) return;
        UpgradeChainUtil.EnqueueOnce(UpgradeChainUtil.GetChooser(), nextUpgrade.Upgrade);
    }

    /// <summary>
    /// Synthesizes one more upgrade as a new sibling under the same parent, rolling a
    /// stat from the owning accessory's allowed family (Accessory.GetUpgradeFamily) so
    /// e.g. a Poison Ring keeps offering Poison Resist rather than switching stats once
    /// its authored chain runs out. Once parented, UpgradeChainUtil.FindNextSibling picks
    /// it up like any authored upgrade on future calls, so no extra bookkeeping is needed
    /// to avoid re-generating it.
    /// </summary>
    /// <summary>How many existing siblings (authored or generated) already have this type.</summary>
    private static int CountExistingTier(Transform parent, UpgradeType type)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var au = parent.GetChild(i).GetComponent<AccessoriesUpgrades>();
            if (au != null && au.upgradeType == type) count++;
        }
        return count;
    }

    /// <summary>
    /// Synthesizes one more upgrade as a new sibling, rolling either a stat from the
    /// accessory's family it doesn't have yet, or - rarer, per UpgradeChainUtil.LevelUpChance -
    /// leveling up one it already owns (scaling the roll by its next tier). For
    /// single-stat families (most rings) every generation after the first is
    /// necessarily a level-up, since there's nothing else in the family to offer.
    /// </summary>
    private AccessoriesUpgrades CreateGeneratedNextUpgrade()
    {
        if (transform.parent == null) return null;
        if (generatedDepth >= MaxGeneratedDepth) return null;

        var family = GetComponent<Accessory>()?.GetUpgradeFamily();
        if (family == null || family.Length == 0) return null;

        // Filter to types Specs actually knows how to roll before picking, rather than
        // picking blind and bailing out - a stray None/Custom in an authored family
        // array would otherwise cost the roll entirely instead of just being ignored.
        List<UpgradeType> rollableFamily = null;
        foreach (var t in family)
        {
            if (!Specs.ContainsKey(t)) continue;
            (rollableFamily ??= new List<UpgradeType>(family.Length)).Add(t);
        }
        if (rollableFamily == null || rollableFamily.Count == 0) return null;

        List<UpgradeType> fresh = null;
        List<UpgradeType> levelable = null;
        foreach (var t in rollableFamily)
        {
            int tier = CountExistingTier(transform.parent, t);
            if (tier <= 0) (fresh ??= new List<UpgradeType>()).Add(t);
            else if (tier < UpgradeChainUtil.MaxUpgradeTier) (levelable ??= new List<UpgradeType>()).Add(t);
        }

        bool hasFresh = fresh != null && fresh.Count > 0;
        bool hasLevelable = levelable != null && levelable.Count > 0;
        if (!hasFresh && !hasLevelable) return null; // everything already taken and maxed

        bool doLevelUp = hasLevelable && (!hasFresh || Random.value < UpgradeChainUtil.LevelUpChance);
        var chosenType = doLevelUp
            ? levelable[Random.Range(0, levelable.Count)]
            : fresh[Random.Range(0, fresh.Count)];

        var spec = Specs[chosenType];
        int newTier = CountExistingTier(transform.parent, chosenType) + 1;
        float raw = Random.Range(spec.RollMin, spec.RollMax);
        float scaled = raw * UpgradeChainUtil.TierMultiplier(newTier);
        float rolled = spec.RollIsInt ? Mathf.Round(scaled) : scaled;

        var go = new GameObject($"{transform.parent.name} - Generated Upgrade");
        go.SetActive(false); // must precede AddComponent so Awake never fires prematurely
        go.transform.SetParent(transform.parent, false);

        var au = go.AddComponent<AccessoriesUpgrades>();
        au.isGenerated = true;
        au.generatedDepth = generatedDepth + 1;
        au.upgradeType = chosenType;
        au.value = rolled;

        string rootName = GetComponent<Accessory>()?.AccesoryName;
        if (string.IsNullOrEmpty(rootName)) rootName = transform.parent.name;

        string displayName = $"{rootName} - {spec.NameSuffix(rolled)}";
        if (newTier > 1) displayName += $" (Tier {UpgradeChainUtil.TierRoman(newTier)})";

        au.Upgrade = new PowerUp
        {
            powerUpName = displayName,
            powerUpDescription = spec.DescFormat(rolled),
            IsWeapon = false,
            IsAccessory = false,
            powerUpObject = go,
            weight = Upgrade != null ? Upgrade.weight : 1f,
        };

        // Give it its own Accessory so its stat line participates in the aggregated
        // stats panel the same way authored steps do (RebuildDescriptionsFromActiveChildren
        // walks Accessory components, not AccessoriesUpgrades).
        var acc = go.AddComponent<Accessory>();
        acc.AccesoryDescription = spec.StatLine(rolled);

        return au;
    }

    /// <summary>
    /// Applies this upgrade's effect directly to the player's SimpleHealth/Snappy2DController
    /// (found once and cached via UpgradeChainUtil), replacing the old per-instance
    /// hand-wired onAwake UnityEvent call.
    /// </summary>
    public void ApplyUpgrade()
    {
        if (upgradeType == UpgradeType.Custom || upgradeType == UpgradeType.None)
            return;

        if (!Specs.TryGetValue(upgradeType, out var spec)) return;
        spec.Apply(value);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(AccessoriesUpgrades))]
    private class AccessoriesUpgradesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var au = (AccessoriesUpgrades)target;
            var so = serializedObject;

            // Always refresh wiring while the inspector is visible
            au.AutoAssignNextUpgrade();

            so.Update();
            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("Upgrade"));

            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.ObjectField(
                    "Next Upgrade (auto)",
                    au.nextUpgrade,
                    typeof(AccessoriesUpgrades),
                    true
                );
            }

            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("upgradeType"));
            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("value"));

            if (au.upgradeType == UpgradeType.Custom)
            {
                UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("onCustomAwake"));
            }

            so.ApplyModifiedProperties();

            if (au.Upgrade != null && Specs.TryGetValue(au.upgradeType, out var spec))
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Preview", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.LabelField("Stat line", spec.StatLine(au.value));
            }
        }
    }
#endif

}
