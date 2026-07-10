using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class ModifierOffer
{
    public UpgradeType Type { get; }
    public string DisplayName { get; }
    public string PreviewText { get; }
    public int Tier { get; }

    internal WeaponRarityController Owner { get; }
    internal IUpgrade Upgrade { get; }
    internal int Seed { get; }
    internal int ModifierRevision { get; }
    internal int Generation { get; }

    internal ModifierOffer(
        WeaponRarityController owner,
        IUpgrade upgrade,
        UpgradeType type,
        string displayName,
        string previewText,
        int tier,
        int seed,
        int modifierRevision,
        int generation)
    {
        Owner = owner;
        Upgrade = upgrade;
        Type = type;
        DisplayName = displayName ?? string.Empty;
        PreviewText = previewText ?? string.Empty;
        Tier = tier;
        Seed = seed;
        ModifierRevision = modifierRevision;
        Generation = generation;
    }
}

[DisallowMultipleComponent]
public class WeaponRarityController : MonoBehaviour, IAccessoryEquipEffect
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
    private bool isAccessory;
    private int modifierRevision;
    private int offerGeneration;

    public int Order => 100;

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

    public IReadOnlyList<UpgradeType> SelectedUpgradeTypes
    {
        get
        {
            var list = new List<UpgradeType>(applied.Count);
            for (int i = 0; i < applied.Count; i++) list.Add(applied[i].type);
            return list;
        }
    }

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
        isAccessory = acc != null;
        var t = GetComponent<WeaponTick>();

        if (k) { knife = new KnifeAdapter(k); uiSink = knife; }
        if (s) { shooter = new ShooterAdapter(s); if (uiSink == null) uiSink = shooter; }
        if (acc) { var accSink = new AccessoryAdapter(acc); if (uiSink == null) uiSink = accSink; }
        if (t) { tick = new TickAdapter(t); }
        if (!healthSystem) healthSystem = GetComponentInParent<SimpleHealth>();
        if (healthSystem) { health = new HealthAdapter(healthSystem); }

        rng = rngSeed == 0 ? new System.Random() : new System.Random(rngSeed);

        if (rollOnAwake && !isAccessory) RerollRarityAndStats();
    }

    public bool HasAvailableModifierOffer => BuildEligibleModifierCandidates().Count > 0;

    public IReadOnlyList<ModifierOffer> CreateModifierOffers(int count = 3)
    {
        EnsureRng();
        offerGeneration++;

        if (count <= 0)
            return Array.Empty<ModifierOffer>();

        var candidates = BuildEligibleModifierCandidates();
        if (candidates.Count == 0)
            return Array.Empty<ModifierOffer>();

        var picked = UpgradeSelectionService.Pick(
            candidates,
            Mathf.Min(count, candidates.Count),
            upgradeWeights,
            rng);

        var offers = new List<ModifierOffer>(picked.Count);
        for (int i = 0; i < picked.Count; i++)
        {
            IUpgrade upgrade = picked[i];
            if (!UpgradeMetadata.TryGet(upgrade, out var metadata)) continue;

            int seed = rng.Next();
            string preview = BuildModifierPreview(upgrade, seed);
            int tier = GetOfferTier(metadata.Type, preview);
            offers.Add(new ModifierOffer(
                this,
                upgrade,
                metadata.Type,
                metadata.Label,
                preview,
                tier,
                seed,
                modifierRevision,
                offerGeneration));
        }

        return offers;
    }

    public bool TryApplyModifierOffer(ModifierOffer offer)
    {
        EnsureRng();
        if (offer == null || offer.Owner != this) return false;
        if (offer.ModifierRevision != modifierRevision || offer.Generation != offerGeneration) return false;
        if (offer.Upgrade == null || HasAppliedType(offer.Type)) return false;
        if (upgradeWeights != null && upgradeWeights.weights.Get(offer.Type) <= 0f) return false;

        var seededContext = BuildContext(new System.Random(offer.Seed));
        if (!offer.Upgrade.IsApplicable(seededContext)) return false;

        int previousCount = applied.Count;
        bool wasFull = previousCount >= RarityRollRules.RollsFor(current);
        AppliedUpgrade added = ApplyUpgrade(seededContext, offer.Upgrade);
        applied.Add(added);

        if (wasFull && previousCount > 0)
        {
            int removedIndex = NextInt(rng, 0, previousCount);
            applied[removedIndex].undo?.Invoke();

            // Keep the chosen modifier in the randomly selected victim's slot.
            // This makes every existing position replaceable instead of always
            // displaying the new modifier at the end of the list.
            applied[removedIndex] = added;
            applied.RemoveAt(applied.Count - 1);
        }

        if (!string.Equals(added.note, offer.PreviewText, StringComparison.Ordinal))
            Debug.LogWarning($"{name}: Applied modifier result differed from its preview.", this);

        FinishAppliedChange();
        return true;
    }

    public bool TryEquip(AccessoryEquipContext context)
    {
        if (!isAccessory) return true;
        if (rollOnAwake) RerollRarityAndStats();
        return true;
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
        var candidates = BuildCandidatesWithoutAoe(ctx);
        if (candidates.Count == 0)
        {
            applied.Clear();
            InvalidateModifierOffers();
            var noUpgradeLines = new List<string>();
            if (current != Rarity.Common)
                noUpgradeLines.Add(WeaponContext.FormatRarity(current));
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
        var candidates = BuildCandidatesWithoutAoe(ctx);
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
        var candidates = BuildCandidatesWithoutAoe(ctx);
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
            InvalidateModifierOffers();
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

        var lines = new List<string>(1 + applied.Count);
        if (current != Rarity.Common)
            lines.Add(WeaponContext.FormatRarity(current));

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
        InvalidateModifierOffers();
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

    private WeaponContext BuildContext(System.Random random = null)
    {
        IDamageModule damage = knife != null ? knife : shooter != null ? shooter : null;
        ICritModule crit = knife != null ? knife : shooter != null ? shooter : null;

        return new WeaponContext
        {
            rng = random ?? rng,
            rarity = current,
            tiers = tiers,
            ranges = ranges,
            sourceObject = gameObject,
            ownerStatusEffects = GetComponentInParent<StatusEffectSystem>(),
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

    private List<UpgradeWeightProvider.Candidate> BuildEligibleModifierCandidates()
    {
        var ctx = BuildContext();
        var candidates = BuildCandidatesWithoutAoe(ctx);
        var existing = new HashSet<UpgradeType>();
        for (int i = 0; i < applied.Count; i++)
            existing.Add(applied[i].type);

        candidates.RemoveAll(candidate =>
            candidate.upgrade == null ||
            existing.Contains(candidate.type) ||
            !candidate.upgrade.IsApplicable(ctx) ||
            (upgradeWeights != null && upgradeWeights.weights.Get(candidate.type) <= 0f));

        return candidates;
    }

    private string BuildModifierPreview(IUpgrade upgrade, int seed)
    {
        var notes = new StringBuilder();
        upgrade.Apply(BuildPreviewContext(seed), notes);
        return notes.ToString().Trim();
    }

    private WeaponContext BuildPreviewContext(int seed)
    {
        WeaponContext live = BuildContext();
        var preview = new PreviewModules(live);

        return new WeaponContext
        {
            rng = new System.Random(seed),
            rarity = current,
            tiers = tiers,
            ranges = ranges,
            isPreview = true,
            sourceObject = gameObject,
            ownerStatusEffects = live.ownerStatusEffects,
            damage = live.damage != null ? preview : null,
            crit = live.crit != null ? preview : null,
            attack = live.attack != null ? preview : null,
            knife = live.knife != null ? preview : null,
            shooter = live.shooter != null ? preview : null,
            health = live.health != null ? preview : null,
            ui = null,
            tickAdapter = null,
        };
    }

    private int GetOfferTier(UpgradeType type, string previewText)
    {
        return type switch
        {
            UpgradeType.DamageFlat => tiers.damageFlat,
            UpgradeType.DamagePercentAsFlat => tiers.damagePercent,
            UpgradeType.AttackSpeed => tiers.attackSpeed,
            UpgradeType.Crit when previewText.IndexOf("Crit Chance", StringComparison.OrdinalIgnoreCase) >= 0 => tiers.critChance,
            UpgradeType.Crit => tiers.critMultiplier,
            UpgradeType.KnifeRadius => tiers.knifeRadius,
            UpgradeType.KnifeSplash => tiers.knifeSplashRadius,
            UpgradeType.KnifeOnslaughtOnKill => tiers.knifeOnslaughtOnKill,
            UpgradeType.ShooterRange => tiers.shooterForce,
            UpgradeType.ShooterAccuracy => tiers.shooterAccuracy,
            UpgradeType.HpFlat => tiers.hpFlat,
            UpgradeType.HpPercent => tiers.hpPercent,
            UpgradeType.HpRegen => tiers.regen,
            UpgradeType.Armor => tiers.armor,
            UpgradeType.Evasion => tiers.evasion,
            UpgradeType.ArmorPercent => tiers.armorPercent,
            UpgradeType.EvasionPercent => tiers.evasionPercent,
            UpgradeType.FireResist or UpgradeType.ColdResist or UpgradeType.LightningResist or UpgradeType.PoisonResist => tiers.resist,
            _ => 1,
        };
    }

    private bool HasAppliedType(UpgradeType type)
    {
        for (int i = 0; i < applied.Count; i++)
            if (applied[i].type == type) return true;
        return false;
    }

    private void EnsureRng()
    {
        rng ??= rngSeed == 0 ? new System.Random() : new System.Random(rngSeed);
    }

    private void InvalidateModifierOffers()
    {
        modifierRevision++;
        offerGeneration++;
    }

    private static List<UpgradeWeightProvider.Candidate> BuildCandidatesWithoutAoe(WeaponContext ctx)
    {
        var candidates = UpgradeCatalog.BuildCandidates(ctx);
        candidates.RemoveAll(candidate => candidate.type == UpgradeType.KnifeSplash);
        return candidates;
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

    private sealed class PreviewModules :
        IDamageModule,
        ICritModule,
        IAttackSpeedModule,
        IKnifeModule,
        IShooterModule,
        IHealthModule
    {
        private int minDamage;
        private int maxDamage;

        public PreviewModules(WeaponContext source)
        {
            if (source.damage != null)
            {
                minDamage = source.damage.MinDamage;
                maxDamage = source.damage.MaxDamage;
            }

            if (source.crit != null)
            {
                CritChance = source.crit.CritChance;
                CritMultiplier = source.crit.CritMultiplier;
            }

            if (source.attack != null)
                Interval = source.attack.Interval;

            if (source.knife != null)
            {
                LifestealPercent = source.knife.LifestealPercent;
                Radius = source.knife.Radius;
                SplashRadius = source.knife.SplashRadius;
                MaxTargetsPerTick = source.knife.MaxTargetsPerTick;
            }

            if (source.shooter != null)
            {
                BulletLifetime = source.shooter.BulletLifetime;
                ShootForce = source.shooter.ShootForce;
                ProjectileCount = source.shooter.ProjectileCount;
                SpreadAngle = source.shooter.SpreadAngle;
            }

            if (source.health != null)
            {
                MaxHealth = source.health.MaxHealth;
                RegenRate = source.health.RegenRate;
                Armor = source.health.Armor;
                Evasion = source.health.Evasion;
                FireResist = source.health.FireResist;
                ColdResist = source.health.ColdResist;
                LightningResist = source.health.LightningResist;
                PoisonResist = source.health.PoisonResist;
            }
        }

        public int Damage { get => MaxDamage; set => MaxDamage = value; }
        public int MinDamage
        {
            get => minDamage;
            set
            {
                minDamage = Mathf.Max(0, value);
                maxDamage = Mathf.Max(minDamage, maxDamage);
            }
        }
        public int MaxDamage { get => maxDamage; set => maxDamage = Mathf.Max(minDamage, value); }
        public float CritChance { get; set; }
        public float CritMultiplier { get; set; }
        public float Interval { get; set; }
        public float LifestealPercent { get; set; }
        public float Radius { get; set; }
        public float SplashRadius { get; set; }
        public int MaxTargetsPerTick { get; set; }
        public float BulletLifetime { get; set; }
        public float ShootForce { get; set; }
        public int ProjectileCount { get; set; }
        public float SpreadAngle { get; set; }
        public int MaxHealth { get; set; }
        public float RegenRate { get; set; }
        public float Armor { get; set; }
        public float Evasion { get; set; }
        public float FireResist { get; set; }
        public float ColdResist { get; set; }
        public float LightningResist { get; set; }
        public float PoisonResist { get; set; }

        public void IncreaseMaxHealth(int delta) => MaxHealth = Mathf.Max(1, MaxHealth + delta);
    }
}
