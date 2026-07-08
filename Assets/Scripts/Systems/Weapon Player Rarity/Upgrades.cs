using System;
using System.Text;
using UnityEngine;

public interface IUpgrade
{
    bool IsApplicable(WeaponContext ctx);
    /// <summary>Apply and return an undo action.</summary>
    Action Apply(WeaponContext ctx, StringBuilder notes);
}

public sealed class WeaponContext
{
    public System.Random rng;
    public Rarity rarity;
    public TierSystem tiers;
    public UpgradeRanges ranges;
    public GameObject sourceObject;
    public StatusEffectSystem ownerStatusEffects;

    // adapters present = supported
    public IDamageModule damage;
    public ICritModule crit;
    public IAttackSpeedModule attack;
    public IKnifeModule knife;
    public IShooterModule shooter;
    public IHealthModule health;
    public IUITextSink ui;                     // sink to write rarity block
    public TickAdapter tickAdapter;            // to reset tick cleanly

    public string Roman(int n)
    {
        n = Mathf.Clamp(n, 1, 5);
        return n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V" };
    }

    public int RangeIntInclusive(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        return rng.Next(minInclusive, maxInclusive + 1);
    }

    public float RangeFloat(float minInclusive, float maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        return minInclusive + (float)rng.NextDouble() * (maxInclusive - minInclusive);
    }

    public bool Chance(float probability) => rng.NextDouble() < Mathf.Clamp01(probability);

    public static string BlueWrap(string inner) => $"<color=#00AEEF>{inner}</color>";
    public static string FormatRarity(Rarity r) => r switch
    {
        Rarity.Common => "<color=#B0B0B0>Common</color>",
        Rarity.Uncommon => "<color=#3EC46D>Uncommon</color>",
        Rarity.Rare => "<color=#3AA0FF>Rare</color>",
        Rarity.Legendary => "<color=#FFB347>Legendary</color>",
        _ => "Common"
    };
}

// ===== Concrete upgrades =====
public sealed class HpFlatUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.hpFlatAdd, c.tiers.hpFlat, 0);
        int add = c.RangeIntInclusive(r.x, r.y);
        c.health.IncreaseMaxHealth(add);
        notes.AppendLine($"+{add} Max Health ({c.Roman(c.tiers.hpFlat)})");
        return () => c.health.IncreaseMaxHealth(-add);
    }
}

public sealed class HpPercentUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.hpMult, c.tiers.hpPercent);
        float mult = c.RangeFloat(r.x, r.y);
        int baseHp = c.health.MaxHealth;
        int delta = Mathf.RoundToInt(baseHp * (mult - 1f));
        if (delta <= 0) delta = 1;
        c.health.IncreaseMaxHealth(delta);
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Max Health ({c.Roman(c.tiers.hpPercent)})");
        return () => c.health.IncreaseMaxHealth(-delta);
    }
}

public sealed class RegenUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.regenAdd, c.tiers.regen);
        float add = Mathf.Max(0f, c.RangeFloat(r.x, r.y));
        float before = c.health.RegenRate;
        float after = Mathf.Max(0f, before + add);
        float actually = after - before;
        c.health.RegenRate = after;
        notes.AppendLine($"+{actually:F2}/s Regen ({c.Roman(c.tiers.regen)})");
        return () => c.health.RegenRate = Mathf.Max(0f, c.health.RegenRate - actually);
    }
}

public sealed class ArmorUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.armorAdd, c.tiers.armor);
        int add = Mathf.Max(0, Mathf.RoundToInt(c.RangeFloat(r.x, r.y)));
        c.health.Armor = Mathf.Max(0f, c.health.Armor + add);
        notes.AppendLine($"+{add} Armor ({c.Roman(c.tiers.armor)})");
        return () => c.health.Armor = Mathf.Max(0f, c.health.Armor - add);
    }
}

public sealed class EvasionUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.evasionAdd, c.tiers.evasion);
        int add = Mathf.Max(0, Mathf.RoundToInt(c.RangeFloat(r.x, r.y)));
        c.health.Evasion = Mathf.Max(0f, c.health.Evasion + add);
        notes.AppendLine($"+{add} Evasion ({c.Roman(c.tiers.evasion)})");
        return () => c.health.Evasion = Mathf.Max(0f, c.health.Evasion - add);
    }
}

public sealed class ArmorPercentUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null && c.health.Armor > 0f;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.armorMult, c.tiers.armorPercent);
        float mult = c.RangeFloat(r.x, r.y);
        float before = Mathf.Max(0f, c.health.Armor);
        if (before <= 0f) { notes.AppendLine("+0% Armor (no base)"); return () => { }; }
        float delta = before * (mult - 1f);
        c.health.Armor = Mathf.Max(0f, before + delta);
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Armor ({c.Roman(c.tiers.armorPercent)})");
        return () => c.health.Armor = Mathf.Max(0f, c.health.Armor - delta);
    }
}

public sealed class EvasionPercentUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.health != null && c.health.Evasion > 0f;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.evasionMult, c.tiers.evasionPercent);
        float mult = c.RangeFloat(r.x, r.y);
        float before = Mathf.Max(0f, c.health.Evasion);
        if (before <= 0f) { notes.AppendLine("+0% Evasion (no base)"); return () => { }; }
        float delta = before * (mult - 1f);
        c.health.Evasion = Mathf.Max(0f, before + delta);
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Evasion ({c.Roman(c.tiers.evasionPercent)})");
        return () => c.health.Evasion = Mathf.Max(0f, c.health.Evasion - delta);
    }
}

public abstract class ResistUpgradeBase : IUpgrade
{
    public abstract string Label { get; }
    protected abstract float Get(WeaponContext c);
    protected abstract void Set(WeaponContext c, float v);
    protected abstract int Tier(WeaponContext c);
    public bool IsApplicable(WeaponContext c) => c.health != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.resistAdd, c.tiers.resist);
        float add = Mathf.Max(0f, c.RangeFloat(r.x, r.y));
        float before = Mathf.Clamp(Get(c), 0f, 0.95f);
        float after = Mathf.Clamp(before + add, 0f, 0.95f);
        float actually = after - before;
        Set(c, after);
        notes.AppendLine($"+{actually * 100f:F0}% {Label} Resist ({c.Roman(Tier(c))})");
        return () => Set(c, Mathf.Clamp(Get(c) - actually, 0f, 0.95f));
    }
}

public sealed class FireResistUpgrade : ResistUpgradeBase
{
    public override string Label => "Fire";
    protected override float Get(WeaponContext c) => c.health.FireResist;
    protected override void Set(WeaponContext c, float v) => c.health.FireResist = v;
    protected override int Tier(WeaponContext c) => c.tiers.resist;
}
public sealed class ColdResistUpgrade : ResistUpgradeBase
{
    public override string Label => "Cold";
    protected override float Get(WeaponContext c) => c.health.ColdResist;
    protected override void Set(WeaponContext c, float v) => c.health.ColdResist = v;
    protected override int Tier(WeaponContext c) => c.tiers.resist;
}
public sealed class LightningResistUpgrade : ResistUpgradeBase
{
    public override string Label => "Lightning";
    protected override float Get(WeaponContext c) => c.health.LightningResist;
    protected override void Set(WeaponContext c, float v) => c.health.LightningResist = v;
    protected override int Tier(WeaponContext c) => c.tiers.resist;
}
public sealed class PoisonResistUpgrade : ResistUpgradeBase
{
    public override string Label => "Poison";
    protected override float Get(WeaponContext c) => c.health.PoisonResist;
    protected override void Set(WeaponContext c, float v) => c.health.PoisonResist = v;
    protected override int Tier(WeaponContext c) => c.tiers.resist;
}
public sealed class DamageFlatUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.damage != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.damageFlatAdd, c.tiers.damageFlat, 0);
        int minAdd = c.RangeIntInclusive(r.x, r.y);
        int maxAdd = c.RangeIntInclusive(r.x, r.y);
        if (minAdd > maxAdd) (minAdd, maxAdd) = (maxAdd, minAdd);

        c.damage.MinDamage += minAdd;
        c.damage.MaxDamage += maxAdd;
        notes.AppendLine($"+{FormatIntRange(minAdd, maxAdd)} Damage ({c.Roman(c.tiers.damageFlat)})");
        return () =>
        {
            c.damage.MinDamage -= minAdd;
            c.damage.MaxDamage -= maxAdd;
        };
    }

    private static string FormatIntRange(int min, int max) => min == max ? min.ToString() : $"{min}-{max}";
}


// Status effect upgrades removed per request.


public sealed class DamagePercentAsFlatUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.damage != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.damageMult, c.tiers.damagePercent);
        float mult = c.RangeFloat(r.x, r.y);

        int baseMin = c.damage.MinDamage;
        int baseMax = c.damage.MaxDamage;
        int minDelta = Mathf.RoundToInt(baseMin * (mult - 1f));
        int maxDelta = Mathf.RoundToInt(baseMax * (mult - 1f));
        c.damage.MinDamage = baseMin + minDelta;
        c.damage.MaxDamage = baseMax + maxDelta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Damage ({c.Roman(c.tiers.damagePercent)})");
        return () =>
        {
            c.damage.MinDamage -= minDelta;
            c.damage.MaxDamage -= maxDelta;
        };
    }
}

public sealed class AttackSpeedUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.attack != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.atkSpeedFrac, c.tiers.attackSpeed);
        float frac = Mathf.Clamp01(c.RangeFloat(r.x, r.y));
        float before = c.attack.Interval;
        float reduceBy = before * frac;
        float newInterval = Mathf.Max(0.05f, before - reduceBy);
        float actuallyReduced = before - newInterval;
        c.attack.Interval = newInterval;
        notes.AppendLine($"+{frac * 100f:F0}% Attack Speed ({c.Roman(c.tiers.attackSpeed)})");
        return () => c.attack.Interval += actuallyReduced;
    }
}

//

// Enables on-hit status and rolls a random allowed status effect (respects blacklist)
//


public sealed class CritUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.crit != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        bool chance = c.Chance(0.6f);
        if (chance)
        {
            var r = c.tiers.Scale(c.ranges.critChanceAdd, c.tiers.critChance);
            float add = Mathf.Clamp01(c.RangeFloat(r.x, r.y));
            c.crit.CritChance = Mathf.Clamp01(c.crit.CritChance + add);
            notes.AppendLine($"+{add * 100f:F0}% Crit Chance ({c.Roman(c.tiers.critChance)})");
            return () => c.crit.CritChance = Mathf.Clamp01(c.crit.CritChance - add);
        }
        else
        {
            var r = c.tiers.Scale(c.ranges.critMultAdd, c.tiers.critMultiplier);
            float add = c.RangeFloat(r.x, r.y);
            c.crit.CritMultiplier += add;
            notes.AppendLine($"+{add:F2} Crit Mult ({c.Roman(c.tiers.critMultiplier)})");
            return () => c.crit.CritMultiplier -= add;
        }
    }
}


public sealed class KnifeRadiusUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.knifeRadiusMult, c.tiers.knifeRadius);
        float mult = c.RangeFloat(r.x, r.y);
        float before = c.knife.Radius;
        float delta = before * (mult - 1f);
        c.knife.Radius = before + delta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% Range ({c.Roman(c.tiers.knifeRadius)})");
        return () => c.knife.Radius -= delta;
    }
}

public sealed class KnifeSplashUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.knife != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.ScaleMultiplierLike(c.ranges.knifeSplashRadiusMult, c.tiers.knifeSplashRadius);
        float mult = c.RangeFloat(r.x, r.y);
        float before = c.knife.SplashRadius;
        float delta = before * (mult - 1f);
        c.knife.SplashRadius = before + delta;
        notes.AppendLine($"+{(mult - 1f) * 100f:F0}% AOE ({c.Roman(c.tiers.knifeSplashRadius)})");
        return () => c.knife.SplashRadius -= delta;
    }
}

public sealed class KnifeOnslaughtOnKillUpgrade : IUpgrade
{
    private const float OnslaughtDuration = 3f;

    public bool IsApplicable(WeaponContext c) => c.knife != null && c.ownerStatusEffects != null && c.rarity >= Rarity.Rare;

    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.knifeOnslaughtOnKillChance, c.tiers.knifeOnslaughtOnKill);
        float chance = Mathf.Clamp01(c.RangeFloat(r.x, r.y));
        GameObject source = c.sourceObject;
        StatusEffectSystem statusEffects = c.ownerStatusEffects;

        void HandleDamageTaken(SimpleHealth.DamageReportEntry entry)
        {
            if (!entry.WasLethal || entry.SourceObject != source)
                return;

            if (UnityEngine.Random.value > chance)
                return;

            statusEffects?.AddStatus(StatusEffectSystem.StatusType.Onslaught, OnslaughtDuration, 1f, source);
        }

        SimpleHealth.AnyDamageTaken += HandleDamageTaken;
        notes.AppendLine($"{chance * 100f:F0}% Onslaught on kill ({c.Roman(c.tiers.knifeOnslaughtOnKill)})");

        return () => SimpleHealth.AnyDamageTaken -= HandleDamageTaken;
    }
}



public sealed class ShooterRangeUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.shooter != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.shooterForceAdd, c.tiers.shooterForce);
        float add = Mathf.Max(0f, c.RangeFloat(r.x, r.y));
        c.shooter.ShootForce += add;
        notes.AppendLine($"+{add:F1} Projectile Speed ({c.Roman(c.tiers.shooterForce)})");
        return () => c.shooter.ShootForce -= add;
    }
}

public sealed class ShooterAccuracyUpgrade : IUpgrade
{
    public bool IsApplicable(WeaponContext c) => c.shooter != null;
    public Action Apply(WeaponContext c, StringBuilder notes)
    {
        var r = c.tiers.Scale(c.ranges.shooterSpreadReduceFrac, c.tiers.shooterAccuracy);
        float frac = Mathf.Clamp01(c.RangeFloat(r.x, r.y));
        float before = c.shooter.SpreadAngle;
        float delta = before * frac;
        float newSpread = Mathf.Max(0f, before - delta);
        float actuallyReduced = before - newSpread;
        c.shooter.SpreadAngle = newSpread;
        notes.AppendLine($"+{frac * 100f:F0}% Accuracy ({c.Roman(c.tiers.shooterAccuracy)})");
        return () => c.shooter.SpreadAngle += actuallyReduced;
    }
}
