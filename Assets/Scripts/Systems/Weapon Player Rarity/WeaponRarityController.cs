using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponRarityController : MonoBehaviour
{
    [Header("Lifecycle")]
    [SerializeField] private bool rollOnAwake = true;
    [SerializeField] private int rngSeed = 0; // 0 = random

    [SerializeField] private UpgradeWeightProvider upgradeWeights;  // optional; if null, fallback to old behavior

    [Header("Rarity")]
    [SerializeField] private Rarity current = Rarity.Common;
    [SerializeField] private RarityWeights weights = new RarityWeights { common = 60, uncommon = 25, rare = 12, legendary = 3 };

    [Header("Ranges")]
    [SerializeField] private UpgradeRanges ranges = new UpgradeRanges();

    [Header("Tiers")]
    [SerializeField] private TierSystem tiers = new TierSystem();

    [Header("References")]
    [SerializeField] private SimpleHealth healthSystem; // optional explicit reference; falls back to parent

    private KnifeAdapter knife;
    private ShooterAdapter shooter;
    private TickAdapter tick;
    private HealthAdapter health;
    private IUITextSink uiSink;
    private System.Random rng;

    [System.Serializable]
    private sealed class AppliedUpgrade
    {
        public UpgradeType type;
        public IUpgrade upgrade;
        public Action undo;
        public string note;
        public int[] tierSlots;

        public AppliedUpgrade(UpgradeType t, IUpgrade u, Action un, string n, int[] slots)
        {
            type = t;
            upgrade = u;
            undo = un;
            note = n;
            tierSlots = slots ?? Array.Empty<int>();
        }
    }

    [SerializeField] private List<AppliedUpgrade> applied = new();

    public int SelectedUpgradeCount => applied.Count;

    public IReadOnlyList<string> SelectedUpgradeNotes
    {
        get
        {
            var list = new List<string>(applied.Count);
            for (int i = 0; i < applied.Count; i++) list.Add(applied[i].note);
            return list;
        }
    }

    private void Awake()
    {
        var k = GetComponent<Knife>();
        var s = GetComponent<SimpleShooter>();
        var acc = GetComponent<Accessory>();
        var t = GetComponent<WeaponTick>();

        if (k) { knife = new KnifeAdapter(k); uiSink = knife; }
        if (s) { shooter = new ShooterAdapter(s); if (uiSink == null) uiSink = shooter; }
        if (acc) { var accSink = new AccessoryAdapter(acc); if (uiSink == null) uiSink = accSink; }
        if (t) { tick = new TickAdapter(t); }
        if (!healthSystem) healthSystem = GetComponentInParent<SimpleHealth>();
        if (healthSystem) { health = new HealthAdapter(healthSystem); }

        rng = rngSeed == 0 ? new System.Random() : new System.Random(rngSeed);

        if (rollOnAwake) RerollRarityAndStats();
    }

    [ContextMenu("Rarity/Reroll Rarity + All Stats")]
    public void RerollRarityAndStats()
    {
        current = weights.Roll(rng);
        RerollStats();
    }

    [ContextMenu("Rarity/Reroll All Stats (keep current rarity)")]
    public void RerollStats()
    {
        tiers.RollAll(rng);
        UndoAllApplied();

        var ctx = BuildContext();
        var candidates = UpgradeCatalog.BuildCandidates(ctx);
        if (candidates.Count == 0)
        {
            applied.Clear();
            var noUpgradeLines = new List<string>
            {
                WeaponContext.FormatRarity(current)
            };
            noUpgradeLines.Add("<i>No applicable upgrades.</i>");
            WriteUIBlock(noUpgradeLines);
            return;
        }

        int rolls = RarityRollRules.RollCountFor(current, rng);
        var picked = UpgradeSelectionService.Pick(candidates, rolls, upgradeWeights, rng);

        ApplyUpgradeListFromCleanState(picked);
        FinishAppliedChange();
    }

    /// <summary>Rerolls a single stat at <paramref name="index"/>. 
    /// If <paramref name="rerollTiers"/> is true, tiers are re-rolled first.</summary>
    public bool RerollStatAt(int index, bool rerollTiers = true)
    {
        if (!IsValidIndex(index)) return false;

        if (rerollTiers)
        {
            var upgrades = CurrentUpgradeInstances();
            tiers.RollAll(rng);
            ApplyUpgradeListFromCleanState(upgrades);
            FinishAppliedChange();
            return true;
        }

        var ctx = BuildContext();
        var prev = applied[index];
        prev.undo?.Invoke();

        applied[index] = ApplyUpgrade(ctx, prev.upgrade);

        FinishAppliedChange();
        return true;
    }

    /// <summary>Rerolls ONE random stat without changing tiers (keeps current tiers).</summary>
    public bool RerollRandomStat()
    {
        if (applied.Count == 0) return false;
        int idx = NextInt(rng, 0, applied.Count);
        return RerollStatAt(idx, false);
    }

    /// <summary>
    /// Removes one random applied upgrade (undoing its effect).
    /// Allows removal down to 1 remaining upgrade, regardless of rarity.
    /// </summary>
    public bool RemoveRandomUpgrade()
    {
        if (applied.Count <= 1) return false;

        int idx = NextInt(rng, 0, applied.Count);
        applied[idx].undo?.Invoke();
        applied.RemoveAt(idx);

        FinishAppliedChange();
        return true;
    }

    public bool AddRandomUpgrade(bool preferUnique = true)
    {
        int maxAllowed = RarityRollRules.RollsFor(current);

        if (applied.Count >= maxAllowed)
        {
            if (applied.Count > 0)
            {
                int index = NextInt(rng, 0, applied.Count);
                return RerollStatIntoAnotherAt(index);
            }
            return false;
        }

        var ctx = BuildContext();
        var candidates = UpgradeCatalog.BuildCandidates(ctx);
        if (candidates == null || candidates.Count == 0) return false;

        IUpgrade picked = UpgradeSelectionService.PickUnique(candidates, AppliedUpgradeTypes(), ctx, upgradeWeights, rng, out bool hasEligibleUnique);
        if (!hasEligibleUnique)
        {
            if (applied.Count > 0)
            {
                int index = NextInt(rng, 0, applied.Count);
                return RerollStatIntoAnotherAt(index);
            }
            return false;
        }

        if (picked == null) return false;

        applied.Add(ApplyUpgrade(ctx, picked));

        FinishAppliedChange();
        return true;
    }

    [ContextMenu("Rarity/Reroll 1 Random Stat")]
    private void ContextRerollOneRandomStat()
    {
        if (!RerollRandomStat())
            Debug.LogWarning($"{name}: No stat to reroll (none applied).");
    }

    public bool RerollStatIntoAnotherAt(int index)
    {
        if (!IsValidIndex(index)) return false;

        var ctx = BuildContext();
        var candidates = UpgradeCatalog.BuildCandidates(ctx);
        if (candidates == null || candidates.Count == 0) return false;

        Type currentType = applied[index].upgrade?.GetType();
        IUpgrade replacement = UpgradeSelectionService.PickReplacement(
            candidates,
            currentType,
            AppliedUpgradeTypesExcept(index),
            ctx,
            upgradeWeights,
            rng);

        if (replacement == null) return false;

        applied[index].undo?.Invoke();
        applied[index] = ApplyUpgrade(ctx, replacement);

        FinishAppliedChange();
        return true;
    }

    /// <summary>Pick a random applied slot and switch it to another upgrade type.</summary>
    public bool RerollRandomStatIntoAnother()
    {
        if (applied.Count == 0) return false;
        int idx = NextInt(rng, 0, applied.Count);
        return RerollStatIntoAnotherAt(idx);
    }

    [ContextMenu("Rarity/Reroll 1 Stat Into Another Type")]
    private void ContextRerollIntoAnother()
    {
        if (!RerollRandomStatIntoAnother())
            Debug.LogWarning($"{name}: No alternative upgrade type available.");
    }

    /// <summary>
    /// Rerolls ALL tier values, then re-applies all current upgrades to reflect the new tiers.
    /// The parameter is ignored; kept for compatibility with existing UI wiring.
    /// </summary>
    public bool RandomizeRandomTier(bool rerollOneAppliedStat = true)
    {
        tiers.RollAll(rng);

        if (applied.Count == 0)
        {
            RebuildUIFromApplied();
            RestartTickIfPlaying();
            return true;
        }

        var upgrades = CurrentUpgradeInstances();
        ApplyUpgradeListFromCleanState(upgrades);
        FinishAppliedChange();
        return true;
    }

    [ContextMenu("Rarity/Tier Upgrade All Modifiers")]
    public bool UpgradeAppliedModifierTiers()
    {
        if (applied.Count == 0) return false;

        var upgrades = CurrentUpgradeInstances();
        var upgradedSlots = new HashSet<int>();
        bool changed = false;

        for (int i = 0; i < applied.Count; i++)
        {
            int[] slots = applied[i].tierSlots;
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                int slot = slots[slotIndex];
                if (!upgradedSlots.Add(slot)) continue;

                changed |= UpgradeCatalog.ImproveTierSlot(tiers, slot, 1);
            }
        }

        if (!changed) return false;

        ApplyUpgradeListFromCleanState(upgrades);
        FinishAppliedChange();
        return true;
    }

    /// <summary>
    /// Upgrades rarity by one step, keeps current stats, and tries to add one unique upgrade.
    /// If already at max rarity, rerolls all modifiers at the current rarity instead.
    /// </summary>
    public bool UpgradeRarityKeepStats()
    {
        var prev = current;
        var next = RarityRollRules.NextRarity(prev);
        if (next == prev)
        {
            RerollStats();
            return true;
        }

        current = next;

        if (!AddRandomUpgrade())
            FinishAppliedChange();
        return true;
    }

    public string GetRangesSummaryText()
    {
        return RarityTextFormatter.BuildRangesSummaryText(current, AppliedUpgradeTypeList(), tiers, ranges);
    }

    /// <summary>
    /// Keeps the weapon/accessory's normal stat card intact and replaces only its
    /// rarity modifier block with the currently selected modifiers' roll ranges.
    /// </summary>
    public string GetStatsTextWithRanges(string normalStatsText)
    {
        return RarityTextFormatter.BuildStatsTextWithRanges(normalStatsText, current, AppliedUpgradeTypeList(), tiers, ranges);
    }

    public void OnDestroy()
    {
        UndoAllApplied();
        applied.Clear();
        applied = null;
    }

    private void RebuildUIFromApplied()
    {
        if (uiSink == null) return;

        var lines = new List<string>(1 + applied.Count)
        {
            WeaponContext.FormatRarity(current)
        };

        for (int i = 0; i < applied.Count; i++)
        {
            string line = applied[i].note;
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        }

        WriteUIBlock(lines);
    }

    private void WriteUIBlock(IReadOnlyList<string> lines)
    {
        if (uiSink == null) return;

        string merged = RarityTextFormatter.MergeRarityBlock(uiSink.Text, lines, current);
        uiSink.SetText(merged);
    }

    private void FinishAppliedChange()
    {
        RebuildUIFromApplied();
        RestartTickIfPlaying();
    }

    private AppliedUpgrade ApplyUpgrade(WeaponContext ctx, IUpgrade up)
    {
        UpgradeType type = default;
        int[] tierSlots = Array.Empty<int>();
        if (UpgradeMetadata.TryGet(up, out var entry))
        {
            type = entry.Type;
            tierSlots = new int[entry.TierSlotCount];
            for (int i = 0; i < tierSlots.Length; i++)
                tierSlots[i] = entry.GetTierSlot(i);
        }

        var sb = new StringBuilder();
        var undo = up.Apply(ctx, sb);
        return new AppliedUpgrade(type, up, undo, sb.ToString().Trim(), tierSlots);
    }

    private void ApplyUpgradeListFromCleanState(IReadOnlyList<IUpgrade> upgrades)
    {
        UndoAllApplied();

        if (upgrades == null || upgrades.Count == 0) return;

        var ctx = BuildContext();
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i] != null)
                applied.Add(ApplyUpgrade(ctx, upgrades[i]));
        }
    }

    private void UndoAllApplied()
    {
        for (int i = applied.Count - 1; i >= 0; i--) applied[i].undo?.Invoke();
        applied.Clear();
    }

    private WeaponContext BuildContext()
    {
        IDamageModule damage = knife != null ? knife : shooter != null ? shooter : null;
        ICritModule crit = knife != null ? knife : shooter != null ? shooter : null;

        return new WeaponContext
        {
            rng = rng,
            rarity = current,
            tiers = tiers,
            ranges = ranges,
            damage = damage,
            crit = crit,
            attack = tick,
            knife = knife,
            shooter = shooter,
            health = health,
            ui = uiSink,
            tickAdapter = tick,
        };
    }

    private List<IUpgrade> CurrentUpgradeInstances()
    {
        var list = new List<IUpgrade>(applied.Count);
        for (int i = 0; i < applied.Count; i++)
            list.Add(applied[i].upgrade);
        return list;
    }

    private IReadOnlyList<UpgradeType> AppliedUpgradeTypeList()
    {
        var list = new List<UpgradeType>(applied.Count);
        for (int i = 0; i < applied.Count; i++)
            list.Add(applied[i].type);
        return list;
    }

    private IEnumerable<IUpgrade> AppliedUpgradeTypes()
    {
        for (int i = 0; i < applied.Count; i++)
            yield return applied[i].upgrade;
    }

    private IEnumerable<IUpgrade> AppliedUpgradeTypesExcept(int excludedIndex)
    {
        for (int i = 0; i < applied.Count; i++)
            if (i != excludedIndex)
                yield return applied[i].upgrade;
    }

    private void RestartTickIfPlaying() => tick?.ResetAndStartIfPlaying();

    private static int NextInt(System.Random r, int minInclusive, int maxExclusive)
        => r.Next(minInclusive, maxExclusive);

    private bool IsValidIndex(int index) => (uint)index < (uint)applied.Count;
}
