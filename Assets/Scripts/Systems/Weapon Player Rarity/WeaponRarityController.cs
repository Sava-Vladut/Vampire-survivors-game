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

    // Cached adapters
    private KnifeAdapter knife;
    private ShooterAdapter shooter;
    private TickAdapter tick;
    private HealthAdapter health;
    private IUITextSink uiSink; // whichever (knife or shooter) we found

    // Single RNG source (deterministic with seed)
    private System.Random rng;

    // ===== Selected Upgrades Tracking =====
    [System.Serializable]
    private sealed class AppliedUpgrade
    {
        public IUpgrade upgrade;   // upgrade behavior
        public Action undo;        // undo action
        public string note;        // human-readable line(s) for UI
        public AppliedUpgrade(IUpgrade u, Action un, string n) { upgrade = u; undo = un; note = n; }
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

    // Wrapper tag constants (NO size tags)
    private const string TAG_COLOR_OPEN = "<color=#00AEEF>";
    private const string TAG_COLOR_CLOSE = "</color>";

    private void Awake()
    {
        // Build adapters (no reflection)
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

    // ===================== Public API =====================

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

        // NEW: build typed candidates instead of bare IUpgrade list
        var candidates = BuildCandidates(ctx);
        if (candidates.Count == 0)
        {
            applied.Clear();
            WriteUIBlock(new[] { $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}", "<i>No applicable upgrades.</i>" });
            return;
        }

        // Instead of fixed max rolls per rarity, roll between 1 and that rarity's max
        int maxRolls = current switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 3,
            Rarity.Legendary => 4,
            _ => 1
        };

        // Randomly choose how many upgrades to apply (at least 1)
        int rolls = rng.Next(1, maxRolls + 1);


        applied.Clear();

        if (upgradeWeights != null)
        {
            // Weighted selection
            var picked = upgradeWeights.PickWeighted(candidates, rolls, rng);
            for (int i = 0; i < picked.Count; i++)
                ApplyAndRecord(ctx, picked[i]);
        }
        else
        {
            // Fallback: old behavior (shuffle + take N)
            Shuffle(candidates, rng);
            for (int i = 0; i < Math.Min(rolls, candidates.Count); i++)
                ApplyAndRecord(ctx, candidates[i].upgrade);
        }

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
    }


    /// <summary>Rerolls a single stat at <paramref name="index"/>. 
    /// If <paramref name="rerollTiers"/> is true, tiers are re-rolled first.</summary>
    public bool RerollStatAt(int index, bool rerollTiers = true)
    {
        if (!IsValidIndex(index)) return false;

        if (rerollTiers)
            tiers.RollAll(rng);

        var ctx = BuildContext();

        var prev = applied[index];
        prev.undo?.Invoke();

        var sb = new StringBuilder();
        var undo = prev.upgrade.Apply(ctx, sb);
        applied[index] = new AppliedUpgrade(prev.upgrade, undo, sb.ToString().Trim());

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }


    /// <summary>Rerolls ONE random stat without changing tiers (keeps current tiers).</summary>
    public bool RerollRandomStat()
    {
        if (applied.Count == 0) return false;
        int idx = NextInt(rng, 0, applied.Count);
        return RerollStatAt(idx, false); // <-- no tier reroll for index 2 button
    }


    /// <summary>
    /// Removes one random applied upgrade (undoing its effect).
    /// Allows removal down to 1 remaining upgrade, regardless of rarity.
    /// </summary>
    public bool RemoveRandomUpgrade()
    {
        // Keep at least one upgrade on the weapon.
        if (applied.Count <= 1) return false;

        int idx = NextInt(rng, 0, applied.Count);
        applied[idx].undo?.Invoke();
        applied.RemoveAt(idx);

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }


    public bool AddRandomUpgrade(bool preferUnique = true)
    {
        int maxAllowed = RollsFor(current);

        // Already at max — replace one upgrade with another type
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
        var cands = BuildCandidates(ctx);
        if (cands == null || cands.Count == 0) return false;

        // Track existing upgrade types
        var existing = new HashSet<System.Type>();
        for (int i = 0; i < applied.Count; i++)
            if (applied[i].upgrade != null)
                existing.Add(applied[i].upgrade.GetType());

        // Find upgrades not already applied (unique)
        var uniqueBag = new List<UpgradeWeightProvider.Candidate>(cands.Count);
        foreach (var cand in cands)
        {
            if (cand.upgrade == null) continue;
            if (!cand.upgrade.IsApplicable(ctx)) continue;
            if (existing.Contains(cand.upgrade.GetType())) continue;
            uniqueBag.Add(cand);
        }

        // If no unique left, just replace one existing upgrade with another type
        if (uniqueBag.Count == 0)
        {
            if (applied.Count > 0)
            {
                int index = NextInt(rng, 0, applied.Count);
                return RerollStatIntoAnotherAt(index);
            }
            return false;
        }

        // Pick a new upgrade and apply
        IUpgrade picked = null;
        if (upgradeWeights != null)
        {
            var list = upgradeWeights.PickWeighted(uniqueBag, 1, rng);
            if (list == null || list.Count == 0 || list[0] == null) return false;
            picked = list[0];
        }
        else
        {
            Shuffle(uniqueBag, rng);
            picked = uniqueBag[0].upgrade;
        }

        var sb = new StringBuilder();
        var undo = picked.Apply(ctx, sb);
        applied.Add(new AppliedUpgrade(picked, undo, sb.ToString().Trim()));

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }







    [ContextMenu("Rarity/Reroll 1 Random Stat")]
    private void ContextRerollOneRandomStat()
    {
        if (!RerollRandomStat())
            Debug.LogWarning($"{name}: No stat to reroll (none applied).");
    }

    // ========== NEW #1: Reroll one stat INTO ANOTHER (switch upgrade type) ==========

    // === Reroll one slot into another UNIQUE upgrade type (no collisions) ===
    public bool RerollStatIntoAnotherAt(int index)
    {
        if (!IsValidIndex(index)) return false;

        tiers.RollAll(rng);
        var ctx = BuildContext();

        var cands = BuildCandidates(ctx);
        if (cands == null || cands.Count == 0) return false;

        var currentType = applied[index].upgrade?.GetType();
        if (currentType == null) return false;

        // Types present in other slots (must avoid)
        var occupied = new HashSet<System.Type>();
        for (int i = 0; i < applied.Count; i++)
        {
            if (i == index) continue;
            var up = applied[i].upgrade;
            if (up != null) occupied.Add(up.GetType());
        }

        // Build alternatives: different type, not occupied, applicable
        var altBag = new List<UpgradeWeightProvider.Candidate>();
        for (int i = 0; i < cands.Count; i++)
        {
            var up = cands[i].upgrade;
            if (up == null) continue;
            var t = up.GetType();
            if (t == currentType) continue;
            if (occupied.Contains(t)) continue;          // enforce uniqueness
            if (!up.IsApplicable(ctx)) continue;
            altBag.Add(cands[i]);
        }

        if (altBag.Count == 0) return false;

        // Choose replacement
        IUpgrade replacement = null;
        if (upgradeWeights != null)
        {
            var list = upgradeWeights.PickWeighted(altBag, 1, rng);
            if (list == null || list.Count == 0 || list[0] == null) return false;
            replacement = list[0];
        }
        else
        {
            Shuffle(altBag, rng);
            replacement = altBag[0].upgrade;
        }

        // Swap
        applied[index].undo?.Invoke();

        var sb = new StringBuilder();
        var undo = replacement.Apply(ctx, sb);
        applied[index] = new AppliedUpgrade(replacement, undo, sb.ToString().Trim());

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
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

    // ========== NEW #2: Reroll all tiers ==========

    /// <summary>
    /// Rerolls ALL tier values, then re-applies all current upgrades to reflect the new tiers.
    /// The parameter is ignored; kept for compatibility with existing UI wiring.
    /// </summary>
    public bool RandomizeRandomTier(bool rerollOneAppliedStat = true)
    {
        // Simplified: reroll ALL tiers at once, then reapply all current upgrades.
        tiers.RollAll(rng);

        if (applied.Count == 0)
        {
            // No applied upgrades to re-roll values for, but still refresh UI to reflect new tiers.
            RebuildUIFromApplied();
            tick?.ResetAndStartIfPlaying();
            return true;
        }

        var ctx = BuildContext();

        var previous = new List<AppliedUpgrade>(applied);

        // Remove every old effect first so percent upgrades calculate from clean base stats.
        for (int i = previous.Count - 1; i >= 0; i--)
            previous[i].undo?.Invoke();

        applied.Clear();

        // Re-apply each selected upgrade with the new tiers (no additional tier rerolls).
        for (int i = 0; i < previous.Count; i++)
        {
            var prev = previous[i];

            var sb = new StringBuilder();
            var undo = prev.upgrade.Apply(ctx, sb);
            applied.Add(new AppliedUpgrade(prev.upgrade, undo, sb.ToString().Trim()));
        }

        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }

    /// <summary>
    /// Upgrades rarity by one step (max Legendary), keeps current stats, and tries to add one unique upgrade.
    /// </summary>
    public bool UpgradeRarityKeepStats()
    {
        var prev = current;
        var next = NextRarity(prev);
        if (next == prev) return false; // already Legendary

        current = next;

        // Optionally roll tiers so future rolls (not current stats) use fresh tiers.
        // This does NOT change already-applied values.
        tiers.RollAll(rng);

        AddRandomUpgrade();

        // Refresh UI & restart tick so the new rarity shows even if no upgrade could be added.
        RebuildUIFromApplied();
        tick?.ResetAndStartIfPlaying();
        return true;
    }

    // ===================== Internal helpers =====================
    private bool RandomizeTierSlot(int slotIndex)
    {
        int newTier = rng.Next(1, 6); // inclusive 1..5
        switch (slotIndex)
        {
            case 0: return SetTier(ref tiers.damagePercent, newTier);
            case 1: return SetTier(ref tiers.damageFlat, newTier);
            case 2: return SetTier(ref tiers.attackSpeed, newTier);
            case 3: return SetTier(ref tiers.critChance, newTier);
            case 4: return SetTier(ref tiers.critMultiplier, newTier);
            case 5: return SetTier(ref tiers.knifeRadius, newTier);
            case 6: return SetTier(ref tiers.knifeSplashRadius, newTier);
            // removed lifesteal and max targets
            case 9: return SetTier(ref tiers.shooterLifetime, newTier);
            case 10: return SetTier(ref tiers.shooterForce, newTier);
            // removed shooter projectiles
            case 12: return SetTier(ref tiers.shooterAccuracy, newTier);
            case 13: return SetTier(ref tiers.hpFlat, newTier);
            case 14: return SetTier(ref tiers.hpPercent, newTier);
            case 15: return SetTier(ref tiers.regen, newTier);
            case 16: return SetTier(ref tiers.armor, newTier);
            case 17: return SetTier(ref tiers.evasion, newTier);
            case 18: return SetTier(ref tiers.resist, newTier);
            case 19: return SetTier(ref tiers.armorPercent, newTier);
            case 20: return SetTier(ref tiers.evasionPercent, newTier);
            default: return false;
        }
    }

    private bool ImproveTierSlot(int slotIndex, int steps)
    {
        switch (slotIndex)
        {
            case 0: return ClampTier(ref tiers.damagePercent, steps);
            case 1: return ClampTier(ref tiers.damageFlat, steps);
            case 2: return ClampTier(ref tiers.attackSpeed, steps);
            case 3: return ClampTier(ref tiers.critChance, steps);
            case 4: return ClampTier(ref tiers.critMultiplier, steps);
            case 5: return ClampTier(ref tiers.knifeRadius, steps);
            case 6: return ClampTier(ref tiers.knifeSplashRadius, steps);
            // removed lifesteal and max targets
            case 9: return ClampTier(ref tiers.shooterLifetime, steps);
            case 10: return ClampTier(ref tiers.shooterForce, steps);
            // removed shooter projectiles
            case 12: return ClampTier(ref tiers.shooterAccuracy, steps);
            case 13: return ClampTier(ref tiers.hpFlat, steps);
            case 14: return ClampTier(ref tiers.hpPercent, steps);
            case 15: return ClampTier(ref tiers.regen, steps);
            case 16: return ClampTier(ref tiers.armor, steps);
            case 17: return ClampTier(ref tiers.evasion, steps);
            case 18: return ClampTier(ref tiers.resist, steps);
            case 19: return ClampTier(ref tiers.armorPercent, steps);
            case 20: return ClampTier(ref tiers.evasionPercent, steps);
            default: return false;
        }
    }

    private int FindAppliedIndexUsingSlot(int slotIndex)
    {
        for (int i = 0; i < applied.Count; i++)
        {
            var list = new List<int>(2);
            CollectSlotsForUpgrade(applied[i].upgrade, list);
            for (int j = 0; j < list.Count; j++)
                if (list[j] == slotIndex) return i;
        }
        return -1;
    }

    private static void CollectSlotsForUpgrade(IUpgrade up, List<int> into)
    {
        if (up == null || into == null) return;
        // Map upgrade type -> related tier slot indices
        if (up is DamageFlatUpgrade) into.Add(1);
        else if (up is DamagePercentAsFlatUpgrade) into.Add(0);
        else if (up is AttackSpeedUpgrade) into.Add(2);
        else if (up is CritUpgrade) { into.Add(3); into.Add(4); }
        else if (up is KnifeRadiusUpgrade) into.Add(5);
        else if (up is KnifeSplashUpgrade) into.Add(6);
        else if (up is ShooterRangeUpgrade) { into.Add(9); into.Add(10); }
        else if (up is ShooterAccuracyUpgrade) into.Add(12);
        else if (up is HpFlatUpgrade) into.Add(13);
        else if (up is HpPercentUpgrade) into.Add(14);
        else if (up is RegenUpgrade) into.Add(15);
        else if (up is ArmorUpgrade) into.Add(16);
        else if (up is EvasionUpgrade) into.Add(17);
        else if (up is FireResistUpgrade || up is ColdResistUpgrade || up is LightningResistUpgrade || up is PoisonResistUpgrade) into.Add(18);
    }

    private static bool SetTier(ref int tierField, int newValue)
    {
        int before = tierField;
        tierField = Mathf.Clamp(newValue, 1, 5);
        return tierField != before;
    }

    private static bool ClampTier(ref int tierField, int steps)
    {
        int before = tierField;
        tierField = Mathf.Clamp(tierField - steps, 1, 5);
        return tierField != before;
    }

    private void ApplyAndRecord(WeaponContext ctx, IUpgrade up)
    {
        var sb = new StringBuilder();
        var undo = up.Apply(ctx, sb);
        applied.Add(new AppliedUpgrade(up, undo, sb.ToString().Trim()));
    }

    private void UndoAllApplied()
    {
        for (int i = applied.Count - 1; i >= 0; i--) applied[i].undo?.Invoke();
        applied.Clear();
    }

    private WeaponContext BuildContext()
    {
        return new WeaponContext
        {
            rng = rng,
            rarity = current,
            tiers = tiers,
            ranges = ranges,
            damage = (IDamageModule)(object)(knife ?? (object)shooter ?? null),
            crit = (ICritModule)(object)(knife ?? (object)shooter ?? null),
            attack = (IAttackSpeedModule)tick,
            knife = knife,
            shooter = shooter,
            health = health,
            ui = uiSink,
            tickAdapter = tick,
        };
    }


    private List<UpgradeWeightProvider.Candidate> BuildCandidates(WeaponContext c)
    {
        var list = new List<UpgradeWeightProvider.Candidate>(12);
        void Add(bool cond, IUpgrade up, UpgradeType t) { if (cond && up != null) list.Add(new UpgradeWeightProvider.Candidate(up, t)); }

        if (c.damage != null)
        {
            Add(true, new DamageFlatUpgrade(), UpgradeType.DamageFlat);
            Add(true, new DamagePercentAsFlatUpgrade(), UpgradeType.DamagePercentAsFlat);
        }

        Add(c.attack != null, new AttackSpeedUpgrade(), UpgradeType.AttackSpeed);
        Add(c.crit != null, new CritUpgrade(), UpgradeType.Crit);

        // Accessories can roll health/defense modifiers, but weapons cannot.
        if (c.health != null && c.knife == null && c.shooter == null)
        {
            Add(true, new HpFlatUpgrade(), UpgradeType.HpFlat);
            Add(true, new HpPercentUpgrade(), UpgradeType.HpPercent);
            Add(true, new RegenUpgrade(), UpgradeType.HpRegen);
            Add(true, new ArmorUpgrade(), UpgradeType.Armor);
            Add(true, new EvasionUpgrade(), UpgradeType.Evasion);
            Add(c.health.Armor > 0f, new ArmorPercentUpgrade(), UpgradeType.ArmorPercent);
            Add(c.health.Evasion > 0f, new EvasionPercentUpgrade(), UpgradeType.EvasionPercent);
            Add(true, new FireResistUpgrade(), UpgradeType.FireResist);
            Add(true, new ColdResistUpgrade(), UpgradeType.ColdResist);
            Add(true, new LightningResistUpgrade(), UpgradeType.LightningResist);
            Add(true, new PoisonResistUpgrade(), UpgradeType.PoisonResist);
        }

        if (c.knife != null)
        {
            Add(true, new KnifeSplashUpgrade(), UpgradeType.KnifeSplash);
            Add(true, new KnifeRadiusUpgrade(), UpgradeType.KnifeRadius);
        }

        if (c.shooter != null)
        {
            Add(true, new ShooterRangeUpgrade(), UpgradeType.ShooterRange);
            Add(true, new ShooterAccuracyUpgrade(), UpgradeType.ShooterAccuracy);
        }

        return list;
    }

    public string GetRangesSummaryText()
    {
        var lines = new List<string>
        {
            "<b>Roll Ranges</b>",
            $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}"
        };

        if (applied == null || applied.Count == 0)
        {
            lines.Add("<i>No selected rolls.</i>");
            return string.Join("\n", lines);
        }

        for (int i = 0; i < applied.Count; i++)
        {
            AddSelectedRangeLine(lines, applied[i].upgrade);
        }

        return string.Join("\n", lines);
    }

    private void AddSelectedRangeLine(List<string> lines, IUpgrade upgrade)
    {
        if (upgrade is DamageFlatUpgrade)
        {
            var r = tiers.Scale(ranges.damageFlatAdd, tiers.damageFlat, 0);
            AddRangeLine(lines, "Damage", r.x, r.y, "", tiers.damageFlat);
        }
        else if (upgrade is DamagePercentAsFlatUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.damageMult, tiers.damagePercent);
            AddPercentRangeLine(lines, "Damage", r.x - 1f, r.y - 1f, tiers.damagePercent);
        }
        else if (upgrade is AttackSpeedUpgrade)
        {
            var r = tiers.Scale(ranges.atkSpeedFrac, tiers.attackSpeed);
            AddPercentRangeLine(lines, "Attack Speed", r.x, r.y, tiers.attackSpeed);
        }
        else if (upgrade is CritUpgrade)
        {
            var chance = tiers.Scale(ranges.critChanceAdd, tiers.critChance);
            var mult = tiers.Scale(ranges.critMultAdd, tiers.critMultiplier);
            AddPercentRangeLine(lines, "Crit Chance", chance.x, chance.y, tiers.critChance);
            AddRangeLine(lines, "Crit Mult", mult.x, mult.y, "", tiers.critMultiplier, "F2");
        }
        else if (upgrade is HpFlatUpgrade)
        {
            var r = tiers.Scale(ranges.hpFlatAdd, tiers.hpFlat, 0);
            AddRangeLine(lines, "Max Health", r.x, r.y, "", tiers.hpFlat);
        }
        else if (upgrade is HpPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.hpMult, tiers.hpPercent);
            AddPercentRangeLine(lines, "Max Health", r.x - 1f, r.y - 1f, tiers.hpPercent);
        }
        else if (upgrade is RegenUpgrade)
        {
            var r = tiers.Scale(ranges.regenAdd, tiers.regen);
            AddRangeLine(lines, "Regen", r.x, r.y, "/s", tiers.regen, "F2");
        }
        else if (upgrade is ArmorUpgrade)
        {
            var r = tiers.Scale(ranges.armorAdd, tiers.armor);
            AddRangeLine(lines, "Armor", r.x, r.y, "", tiers.armor);
        }
        else if (upgrade is EvasionUpgrade)
        {
            var r = tiers.Scale(ranges.evasionAdd, tiers.evasion);
            AddRangeLine(lines, "Evasion", r.x, r.y, "", tiers.evasion);
        }
        else if (upgrade is ArmorPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.armorMult, tiers.armorPercent);
            AddPercentRangeLine(lines, "Armor", r.x - 1f, r.y - 1f, tiers.armorPercent);
        }
        else if (upgrade is EvasionPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.evasionMult, tiers.evasionPercent);
            AddPercentRangeLine(lines, "Evasion", r.x - 1f, r.y - 1f, tiers.evasionPercent);
        }
        else if (upgrade is FireResistUpgrade || upgrade is ColdResistUpgrade || upgrade is LightningResistUpgrade || upgrade is PoisonResistUpgrade)
        {
            var r = tiers.Scale(ranges.resistAdd, tiers.resist);
            AddPercentRangeLine(lines, "Resist", r.x, r.y, tiers.resist);
        }
        else if (upgrade is KnifeRadiusUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.knifeRadiusMult, tiers.knifeRadius);
            AddPercentRangeLine(lines, "Range", r.x - 1f, r.y - 1f, tiers.knifeRadius);
        }
        else if (upgrade is KnifeSplashUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.knifeSplashRadiusMult, tiers.knifeSplashRadius);
            AddPercentRangeLine(lines, "AOE", r.x - 1f, r.y - 1f, tiers.knifeSplashRadius);
        }
        else if (upgrade is ShooterRangeUpgrade)
        {
            var r = tiers.Scale(ranges.shooterForceAdd, tiers.shooterForce);
            AddRangeLine(lines, "Projectile Speed", r.x, r.y, "", tiers.shooterForce, "F1");
        }
        else if (upgrade is ShooterAccuracyUpgrade)
        {
            var r = tiers.Scale(ranges.shooterSpreadReduceFrac, tiers.shooterAccuracy);
            AddPercentRangeLine(lines, "Accuracy", r.x, r.y, tiers.shooterAccuracy);
        }
    }

    private static void AddRangeLine(List<string> lines, string label, int min, int max, string suffix, int tier)
    {
        lines.Add($"+{min}-{max}{suffix} {label} ({RomanStatic(tier)})");
    }

    private static void AddRangeLine(List<string> lines, string label, float min, float max, string suffix, int tier, string format = "F0")
    {
        if (min > max) (min, max) = (max, min);
        lines.Add($"+{min.ToString(format)}-{max.ToString(format)}{suffix} {label} ({RomanStatic(tier)})");
    }

    private static void AddPercentRangeLine(List<string> lines, string label, float min, float max, int tier)
    {
        if (min > max) (min, max) = (max, min);
        lines.Add($"+{min * 100f:F0}-{max * 100f:F0}% {label} ({RomanStatic(tier)})");
    }

    private static string RomanStatic(int n)
    {
        n = Mathf.Clamp(n, 1, 5);
        return n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V" };
    }





    private void RebuildUIFromApplied()
    {
        if (uiSink == null) return;

        var lines = new List<string>(1 + applied.Count)
        {
            $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}"
        };
        for (int i = 0; i < applied.Count; i++)
        {
            var line = applied[i].note;
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        }

        WriteUIBlock(lines);
    }

    public void OnDestroy()
    {
        UndoAllApplied();
        applied.Clear();
        applied = null;
    }

    private void WriteUIBlock(IReadOnlyList<string> lines)
    {
        if (uiSink == null) return;

        // Build block text
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            var clean = StripWrappers(lines[i]);
            if (!string.IsNullOrWhiteSpace(clean))
            {
                sb.AppendLine(clean);
            }
        }
        string inner = sb.ToString().TrimEnd();

        // Wrap exactly once with color
        string block = TAG_COLOR_OPEN + inner + TAG_COLOR_CLOSE;

        // Remove last rarity section (if any), then append
        string cur = uiSink.Text ?? string.Empty;
        string withoutLast = RemoveLastRaritySection(cur);

        string merged = string.IsNullOrWhiteSpace(withoutLast)
                        ? block
                        : (withoutLast.TrimEnd() + "\n" + block);

        merged = DedupeColorTags(NormalizeWhitespace(merged));

        uiSink.SetText(merged);
    }

    // ===================== Text utils =====================

    private static string StripWrappers(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("<size=80%>", string.Empty).Replace("</size>", string.Empty);
        s = s.Replace(TAG_COLOR_OPEN, string.Empty).Replace(TAG_COLOR_CLOSE, string.Empty);
        return s.Trim();
    }

    private static string RemoveLastRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        int rarityIdx = s.LastIndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase);
        if (rarityIdx < 0)
        {
            int colorIdx = s.LastIndexOf(TAG_COLOR_OPEN, StringComparison.OrdinalIgnoreCase);
            return (colorIdx >= 0) ? s[..colorIdx].TrimEnd() : s;
        }

        int colorStart = s.LastIndexOf(TAG_COLOR_OPEN, rarityIdx, StringComparison.OrdinalIgnoreCase);
        int startIdx = (colorStart >= 0) ? colorStart : rarityIdx;
        return s[..startIdx].TrimEnd();
    }

    private static string DedupeColorTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Two quick passes are plenty for our merges
        s = s.Replace(TAG_COLOR_OPEN + TAG_COLOR_OPEN, TAG_COLOR_OPEN);
        s = s.Replace(TAG_COLOR_CLOSE + TAG_COLOR_CLOSE, TAG_COLOR_CLOSE);
        return s;
    }

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        int nlRun = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '\r') continue; // normalize CRLF -> LF

            if (c == '\n')
            {
                nlRun++;
                if (nlRun <= 2) sb.Append('\n');
                continue;
            }

            nlRun = 0;

            // Drop leading spaces/tabs at line starts
            if ((c == ' ' || c == '\t') && sb.Length > 0 && sb[^1] == '\n') continue;

            sb.Append(c);
        }

        return sb.ToString().TrimEnd();
    }

    // ===================== RNG helpers =====================

    private static Rarity NextRarity(Rarity r) => r switch
    {
        Rarity.Common => Rarity.Uncommon,
        Rarity.Uncommon => Rarity.Rare,
        Rarity.Rare => Rarity.Legendary,
        _ => Rarity.Legendary
    };

    private static int RollsFor(Rarity r) => r switch
    {
        Rarity.Common => 1,
        Rarity.Uncommon => 2,
        Rarity.Rare => 3,
        Rarity.Legendary => 4,
        _ => 1
    };


    private static int NextInt(System.Random r, int minInclusive, int maxExclusive)
        => r.Next(minInclusive, maxExclusive);

    private static void Shuffle<T>(IList<T> list, System.Random r)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = r.Next(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool IsValidIndex(int index) => (uint)index < (uint)applied.Count;
}
