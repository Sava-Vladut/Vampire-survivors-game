using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Public enum to tag each concrete upgrade type (no reflection).
/// </summary>
public enum UpgradeType
{
    DamageFlat,
    DamagePercentAsFlat,
    AttackSpeed,
    Crit,
    // Health/Defense
    HpFlat,
    HpPercent,
    HpRegen,
    Armor,
    Evasion,
    ArmorPercent,
    EvasionPercent,
    FireResist,
    ColdResist,
    LightningResist,
    PoisonResist,
    KnifeRadius,
    KnifeSplash,
    
    ShooterRange,
    ShooterAccuracy,
    KnifeOnslaughtOnKill,
    // Mana values are appended to preserve existing serialized enum IDs.
    ManaCost,
    ManaMax,
    ManaRegen
}

/// <summary>
/// Inspector-friendly table of weights per UpgradeType.
/// Weight <= 0 disables that upgrade from being picked.
/// </summary>
[Serializable]
public class UpgradeWeightTable
{
    [Header("Shared")]
    [Min(0f)] public float damageFlat = 1f;
    [Min(0f)] public float damagePercentAsFlat = 1f;
    [Min(0f)] public float attackSpeed = 1f;
    [Min(0f)] public float crit = 1f;

    [Header("Mana (eligible weapons only)")]
    [Min(0f)] public float manaCost = 1f;
    [Min(0f)] public float manaMax = 1f;
    [Min(0f)] public float manaRegen = 1f;

    [Header("Health / Defense")]
    [Min(0f)] public float hpFlat = 1f;
    [Min(0f)] public float hpPercent = 1f;
    [Min(0f)] public float hpRegen = 1f;
    [Min(0f)] public float armor = 1f;
    [Min(0f)] public float evasion = 1f;
    [Min(0f)] public float armorPercent = 1f;
    [Min(0f)] public float evasionPercent = 1f;
    [Min(0f)] public float fireResist = 0.5f;
    [Min(0f)] public float coldResist = 0.5f;
    [Min(0f)] public float lightningResist = 0.5f;
    [Min(0f)] public float poisonResist = 0.5f;

    [Header("Knife")]
    [Min(0f)] public float knifeRadius = 1f;
    [Min(0f)] public float knifeSplash = 1f;
    [Min(0f)] public float knifeOnslaughtOnKill = 1f;
    

    [Header("Shooter")]
    [Min(0f)] public float shooterRange = 1f;        // covers lifetime/force branch
    [Min(0f)] public float shooterAccuracy = 1f;

    public float Get(UpgradeType t) => t switch
    {
        UpgradeType.DamageFlat => damageFlat,
        UpgradeType.DamagePercentAsFlat => damagePercentAsFlat,
        UpgradeType.AttackSpeed => attackSpeed,
        UpgradeType.Crit => crit,
        UpgradeType.ManaCost => manaCost,
        UpgradeType.ManaMax => manaMax,
        UpgradeType.ManaRegen => manaRegen,
        UpgradeType.HpFlat => hpFlat,
        UpgradeType.HpPercent => hpPercent,
        UpgradeType.HpRegen => hpRegen,
        UpgradeType.Armor => armor,
        UpgradeType.Evasion => evasion,
        UpgradeType.ArmorPercent => armorPercent,
        UpgradeType.EvasionPercent => evasionPercent,
        UpgradeType.FireResist => fireResist,
        UpgradeType.ColdResist => coldResist,
        UpgradeType.LightningResist => lightningResist,
        UpgradeType.PoisonResist => poisonResist,
        UpgradeType.KnifeRadius => knifeRadius,
        UpgradeType.KnifeSplash => knifeSplash,
        UpgradeType.KnifeOnslaughtOnKill => knifeOnslaughtOnKill,
        
        UpgradeType.ShooterRange => shooterRange,
        UpgradeType.ShooterAccuracy => shooterAccuracy,
        _ => 0f
    };
}

/// <summary>
/// Add this component next to WeaponRarityController to make its upgrade picks weighted.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeWeightProvider : MonoBehaviour
{
    [Tooltip("Per-upgrade weights. 0 = disable that upgrade.")]
    public UpgradeWeightTable weights = new UpgradeWeightTable();

    /// <summary>
    /// Candidate wrapper so we can keep the upgrade behavior and its tag together.
    /// </summary>
    public readonly struct Candidate
    {
        public readonly IUpgrade upgrade;
        public readonly UpgradeType type;
        public Candidate(IUpgrade up, UpgradeType t) { upgrade = up; type = t; }
    }

    /// <summary>
    /// Weighted sampling without replacement. Returns up to 'picks' upgrades.
    /// If all weights are zero, returns empty list.
    /// </summary>
    public List<IUpgrade> PickWeighted(IList<Candidate> candidates, int picks, System.Random rng)
    {
        var bag = new List<Candidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.upgrade == null) continue;
            if (weights.Get(c.type) > 0f) bag.Add(c);
        }

        var result = new List<IUpgrade>(Mathf.Min(picks, bag.Count));
        picks = Mathf.Min(picks, bag.Count);
        if (picks <= 0) return result;

        // Copy weights so we can remove as we pick
        var w = new List<float>(bag.Count);
        float total = 0f;
        for (int i = 0; i < bag.Count; i++)
        {
            float wi = Mathf.Max(0f, weights.Get(bag[i].type));
            w.Add(wi);
            total += wi;
        }
        if (total <= 0f) return result;

        // Sample without replacement
        for (int p = 0; p < picks; p++)
        {
            double r = rng.NextDouble() * total;
            int chosen = -1;
            for (int i = 0; i < w.Count; i++)
            {
                r -= w[i];
                if (r <= 0.0)
                {
                    chosen = i;
                    break;
                }
            }
            if (chosen < 0) chosen = w.Count - 1;

            result.Add(bag[chosen].upgrade);

            // remove chosen
            total -= w[chosen];
            bag.RemoveAt(chosen);
            w.RemoveAt(chosen);

            if (bag.Count == 0 || total <= 0f) break;
        }

        return result;
    }
}
