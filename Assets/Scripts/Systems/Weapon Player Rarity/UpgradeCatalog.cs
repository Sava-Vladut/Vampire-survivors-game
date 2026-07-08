using System.Collections.Generic;
using UnityEngine;

public static class UpgradeCatalog
{
    public static List<UpgradeWeightProvider.Candidate> BuildCandidates(WeaponContext c)
    {
        var list = new List<UpgradeWeightProvider.Candidate>(12);
        void Add(bool cond, IUpgrade up, UpgradeType t)
        {
            if (cond && up != null) list.Add(new UpgradeWeightProvider.Candidate(up, t));
        }

        if (c.damage != null)
        {
            Add(true, new DamageFlatUpgrade(), UpgradeType.DamageFlat);
            Add(true, new DamagePercentAsFlatUpgrade(), UpgradeType.DamagePercentAsFlat);
        }

        Add(c.attack != null, new AttackSpeedUpgrade(), UpgradeType.AttackSpeed);
        Add(c.crit != null, new CritUpgrade(), UpgradeType.Crit);

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
            Add(c.rarity >= Rarity.Rare && c.ownerStatusEffects != null, new KnifeOnslaughtOnKillUpgrade(), UpgradeType.KnifeOnslaughtOnKill);
        }

        if (c.shooter != null)
        {
            Add(true, new ShooterRangeUpgrade(), UpgradeType.ShooterRange);
            Add(true, new ShooterAccuracyUpgrade(), UpgradeType.ShooterAccuracy);
        }

        return list;
    }

    public static bool RandomizeTierSlot(TierSystem tiers, int slotIndex, System.Random rng)
    {
        int newTier = rng.Next(1, 6);
        return SetTierSlot(tiers, slotIndex, newTier);
    }

    public static bool ImproveTierSlot(TierSystem tiers, int slotIndex, int steps)
    {
        if (!TryGetTierSlot(tiers, slotIndex, out int before)) return false;
        return SetTierSlot(tiers, slotIndex, Mathf.Clamp(before - steps, 1, 5));
    }

    public static void CollectSlotsForUpgrade(IUpgrade up, List<int> into)
    {
        if (up == null || into == null) return;

        if (!UpgradeMetadata.TryGet(up, out var entry)) return;
        for (int i = 0; i < entry.TierSlotCount; i++)
            into.Add(entry.GetTierSlot(i));
    }

    private static bool TryGetTierSlot(TierSystem tiers, int slotIndex, out int value)
    {
        value = slotIndex switch
        {
            0 => tiers.damagePercent,
            1 => tiers.damageFlat,
            2 => tiers.attackSpeed,
            3 => tiers.critChance,
            4 => tiers.critMultiplier,
            5 => tiers.knifeRadius,
            6 => tiers.knifeSplashRadius,
            7 => tiers.knifeOnslaughtOnKill,
            9 => tiers.shooterLifetime,
            10 => tiers.shooterForce,
            12 => tiers.shooterAccuracy,
            13 => tiers.hpFlat,
            14 => tiers.hpPercent,
            15 => tiers.regen,
            16 => tiers.armor,
            17 => tiers.evasion,
            18 => tiers.resist,
            19 => tiers.armorPercent,
            20 => tiers.evasionPercent,
            _ => 0
        };

        return slotIndex is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 9 or 10 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;
    }

    private static bool SetTierSlot(TierSystem tiers, int slotIndex, int newValue)
    {
        newValue = Mathf.Clamp(newValue, 1, 5);
        switch (slotIndex)
        {
            case 0: return SetTier(ref tiers.damagePercent, newValue);
            case 1: return SetTier(ref tiers.damageFlat, newValue);
            case 2: return SetTier(ref tiers.attackSpeed, newValue);
            case 3: return SetTier(ref tiers.critChance, newValue);
            case 4: return SetTier(ref tiers.critMultiplier, newValue);
            case 5: return SetTier(ref tiers.knifeRadius, newValue);
            case 6: return SetTier(ref tiers.knifeSplashRadius, newValue);
            case 7: return SetTier(ref tiers.knifeOnslaughtOnKill, newValue);
            case 9: return SetTier(ref tiers.shooterLifetime, newValue);
            case 10: return SetTier(ref tiers.shooterForce, newValue);
            case 12: return SetTier(ref tiers.shooterAccuracy, newValue);
            case 13: return SetTier(ref tiers.hpFlat, newValue);
            case 14: return SetTier(ref tiers.hpPercent, newValue);
            case 15: return SetTier(ref tiers.regen, newValue);
            case 16: return SetTier(ref tiers.armor, newValue);
            case 17: return SetTier(ref tiers.evasion, newValue);
            case 18: return SetTier(ref tiers.resist, newValue);
            case 19: return SetTier(ref tiers.armorPercent, newValue);
            case 20: return SetTier(ref tiers.evasionPercent, newValue);
            default: return false;
        }
    }

    private static bool SetTier(ref int tierField, int newValue)
    {
        int before = tierField;
        tierField = newValue;
        return tierField != before;
    }
}

public readonly struct UpgradeMetadataEntry
{
    public readonly UpgradeType Type;
    public readonly string Label;
    private readonly int slot0;
    private readonly int slot1;
    private readonly int slotCount;

    public int TierSlotCount => slotCount;

    public UpgradeMetadataEntry(UpgradeType type, string label, int firstSlot, int secondSlot = -1)
    {
        Type = type;
        Label = label;
        slot0 = firstSlot;
        slot1 = secondSlot;
        slotCount = secondSlot >= 0 ? 2 : firstSlot >= 0 ? 1 : 0;
    }

    public int GetTierSlot(int index) => index == 0 ? slot0 : slot1;
}

public static class UpgradeMetadata
{
    public static bool TryGet(IUpgrade upgrade, out UpgradeMetadataEntry entry)
    {
        entry = default;
        if (upgrade == null) return false;

        if (upgrade is DamageFlatUpgrade) return Set(out entry, UpgradeType.DamageFlat, "Damage", 1);
        if (upgrade is DamagePercentAsFlatUpgrade) return Set(out entry, UpgradeType.DamagePercentAsFlat, "Damage", 0);
        if (upgrade is AttackSpeedUpgrade) return Set(out entry, UpgradeType.AttackSpeed, "Attack Speed", 2);
        if (upgrade is CritUpgrade) return Set(out entry, UpgradeType.Crit, "Crit", 3, 4);
        if (upgrade is KnifeRadiusUpgrade) return Set(out entry, UpgradeType.KnifeRadius, "Range", 5);
        if (upgrade is KnifeSplashUpgrade) return Set(out entry, UpgradeType.KnifeSplash, "AOE", 6);
        if (upgrade is KnifeOnslaughtOnKillUpgrade) return Set(out entry, UpgradeType.KnifeOnslaughtOnKill, "Onslaught On Kill", 7);
        if (upgrade is ShooterRangeUpgrade) return Set(out entry, UpgradeType.ShooterRange, "Projectile Speed", 9, 10);
        if (upgrade is ShooterAccuracyUpgrade) return Set(out entry, UpgradeType.ShooterAccuracy, "Accuracy", 12);
        if (upgrade is HpFlatUpgrade) return Set(out entry, UpgradeType.HpFlat, "Max Health", 13);
        if (upgrade is HpPercentUpgrade) return Set(out entry, UpgradeType.HpPercent, "Max Health", 14);
        if (upgrade is RegenUpgrade) return Set(out entry, UpgradeType.HpRegen, "Regen", 15);
        if (upgrade is ArmorUpgrade) return Set(out entry, UpgradeType.Armor, "Armor", 16);
        if (upgrade is EvasionUpgrade) return Set(out entry, UpgradeType.Evasion, "Evasion", 17);
        if (upgrade is FireResistUpgrade) return Set(out entry, UpgradeType.FireResist, "Fire Resist", 18);
        if (upgrade is ColdResistUpgrade) return Set(out entry, UpgradeType.ColdResist, "Cold Resist", 18);
        if (upgrade is LightningResistUpgrade) return Set(out entry, UpgradeType.LightningResist, "Lightning Resist", 18);
        if (upgrade is PoisonResistUpgrade) return Set(out entry, UpgradeType.PoisonResist, "Poison Resist", 18);
        if (upgrade is ArmorPercentUpgrade) return Set(out entry, UpgradeType.ArmorPercent, "Armor", 19);
        if (upgrade is EvasionPercentUpgrade) return Set(out entry, UpgradeType.EvasionPercent, "Evasion", 20);

        return false;
    }

    public static bool TryGet(UpgradeType type, out UpgradeMetadataEntry entry)
    {
        entry = type switch
        {
            UpgradeType.DamageFlat => new UpgradeMetadataEntry(type, "Damage", 1),
            UpgradeType.DamagePercentAsFlat => new UpgradeMetadataEntry(type, "Damage", 0),
            UpgradeType.AttackSpeed => new UpgradeMetadataEntry(type, "Attack Speed", 2),
            UpgradeType.Crit => new UpgradeMetadataEntry(type, "Crit", 3, 4),
            UpgradeType.KnifeRadius => new UpgradeMetadataEntry(type, "Range", 5),
            UpgradeType.KnifeSplash => new UpgradeMetadataEntry(type, "AOE", 6),
            UpgradeType.KnifeOnslaughtOnKill => new UpgradeMetadataEntry(type, "Onslaught On Kill", 7),
            UpgradeType.ShooterRange => new UpgradeMetadataEntry(type, "Projectile Speed", 9, 10),
            UpgradeType.ShooterAccuracy => new UpgradeMetadataEntry(type, "Accuracy", 12),
            UpgradeType.HpFlat => new UpgradeMetadataEntry(type, "Max Health", 13),
            UpgradeType.HpPercent => new UpgradeMetadataEntry(type, "Max Health", 14),
            UpgradeType.HpRegen => new UpgradeMetadataEntry(type, "Regen", 15),
            UpgradeType.Armor => new UpgradeMetadataEntry(type, "Armor", 16),
            UpgradeType.Evasion => new UpgradeMetadataEntry(type, "Evasion", 17),
            UpgradeType.FireResist => new UpgradeMetadataEntry(type, "Fire Resist", 18),
            UpgradeType.ColdResist => new UpgradeMetadataEntry(type, "Cold Resist", 18),
            UpgradeType.LightningResist => new UpgradeMetadataEntry(type, "Lightning Resist", 18),
            UpgradeType.PoisonResist => new UpgradeMetadataEntry(type, "Poison Resist", 18),
            UpgradeType.ArmorPercent => new UpgradeMetadataEntry(type, "Armor", 19),
            UpgradeType.EvasionPercent => new UpgradeMetadataEntry(type, "Evasion", 20),
            _ => default
        };

        return entry.TierSlotCount > 0;
    }

    private static bool Set(out UpgradeMetadataEntry entry, UpgradeType type, string label, int firstSlot, int secondSlot = -1)
    {
        entry = new UpgradeMetadataEntry(type, label, firstSlot, secondSlot);
        return true;
    }
}
