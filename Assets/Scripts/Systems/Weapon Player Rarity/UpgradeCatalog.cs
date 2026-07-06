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
        else if (up is ArmorPercentUpgrade) into.Add(19);
        else if (up is EvasionPercentUpgrade) into.Add(20);
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

        return slotIndex is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 9 or 10 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;
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
