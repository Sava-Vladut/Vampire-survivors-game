using System;
using UnityEngine;

public static class ShooterUpgradeDefinitions
{
    private const WeaponUpgradeTraits Generated =
        WeaponUpgradeTraits.Generated | WeaponUpgradeTraits.ScalesWithRarity;
    private const float Epsilon = 0.0001f;

    public static void Register(WeaponUpgradeCatalogBuilder builder)
    {
        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterDamageFlat,
            "Tempered Power +{0}", "Each hit deals {0} more damage.",
            WeaponUpgradeValueFormat.Integer, 1f, 6f, true,
            (shooter, value) =>
            {
                int amount = Mathf.RoundToInt(value);
                shooter.minDamage += amount;
                shooter.damage += amount;
            }));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterDamagePercent,
            "Weapon Mastery +{0}", "All damage from this weapon is increased by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.12f, false,
            (shooter, value) =>
            {
                shooter.minDamage = Mathf.RoundToInt(shooter.minDamage * (1f + value));
                shooter.damage = Mathf.RoundToInt(shooter.damage * (1f + value));
            },
            shooter => shooter.damage > 0));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.ShooterDamageTypeIndex,
            "{0} Infusion", "Attunes every hit from this weapon to {0} damage.",
            WeaponUpgradeValueFormat.DamageType,
            WeaponUpgradeTraits.Generated,
            shooter => WeaponUpgradeRollUtility.RollDifferentDamageType(shooter.damageType),
            (shooter, application) =>
                shooter.damageType = WeaponUpgradeRollUtility.ResolveDifferentDamageType(shooter.damageType, application.Value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterProjectileCount,
            "Multishot +{0}", "Fires {0} additional projectiles with every attack.",
            WeaponUpgradeValueFormat.Integer, 1f, 2f, true,
            (shooter, value) => shooter.projectileCount += Mathf.RoundToInt(value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterSpreadAngleFlat,
            "Steady Aim +{0}", "Reduces projectile spread by {0}, making shots more accurate.",
            WeaponUpgradeValueFormat.Degrees, 1f, 10f, false,
            (shooter, value) => shooter.spreadAngle = Mathf.Max(0f, shooter.spreadAngle - value),
            shooter => shooter.spreadAngle > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterSpreadAnglePercent,
            "Deadeye +{0}", "Reduces current projectile spread by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f, false,
            (shooter, value) => shooter.spreadAngle = Mathf.Max(0f, shooter.spreadAngle * (1f - value)),
            shooter => shooter.spreadAngle > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterProjectileSpeedFlat,
            "Swift Projectiles +{0}", "Projectiles travel {0} faster.",
            WeaponUpgradeValueFormat.Decimal1, 0.25f, 2.5f, false,
            (shooter, value) => shooter.shootForce += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterProjectileSpeedPercent,
            "Arcane Velocity +{0}", "Increases projectile speed by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f, false,
            (shooter, value) => shooter.shootForce *= 1f + value,
            shooter => shooter.shootForce > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterLifetimeFlat,
            "Extended Flight +{0}", "Projectiles remain active for {0} longer.",
            WeaponUpgradeValueFormat.Seconds1, 0.15f, 1f, false,
            (shooter, value) => shooter.bulletLifetime += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterLifetimePercent,
            "Unfading Shot +{0}", "Increases projectile lifetime by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f, false,
            (shooter, value) => shooter.bulletLifetime *= 1f + value,
            shooter => shooter.bulletLifetime > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterCritChanceFlat,
            "Keen Edge +{0}", "Grants {0} additional critical-hit chance.",
            WeaponUpgradeValueFormat.Percent, 0.02f, 0.1f, false,
            (shooter, value) => shooter.critChance = Mathf.Clamp01(shooter.critChance + value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterCritMultiplierFlat,
            "Savage Criticals +{0}", "Critical hits gain {0} additional damage multiplier.",
            WeaponUpgradeValueFormat.Multiplier, 0.05f, 0.3f, false,
            (shooter, value) => shooter.critMultiplier += value));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.ShooterStatusApplyChanceFlat,
            "Affliction Chance +{0}", "Hits are {0} more likely to inflict their on-hit effect.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.15f,
            (shooter, value) => shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance + value)));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.ShooterStatusApplyChancePercent,
            "Potent Affliction +{0}", "Multiplies this weapon's on-hit effect chance by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f,
            (shooter, value) => shooter.statusApplyChance = Mathf.Clamp01(shooter.statusApplyChance * (1f + value)),
            shooter => shooter.statusApplyChance > Epsilon));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.ShooterStatusDurationFlat,
            "Lingering Curse +{0}", "On-hit effects remain on enemies for {0} longer.",
            WeaponUpgradeValueFormat.Seconds1, 0.25f, 1.5f,
            (shooter, value) => shooter.statusEffectDuration += value));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.ShooterStatusDurationPercent,
            "Enduring Affliction +{0}", "Increases this weapon's on-hit effect duration by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f,
            (shooter, value) => shooter.statusEffectDuration *= 1f + value,
            shooter => shooter.statusEffectDuration > Epsilon));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.ShooterEnableStatusEffect,
            "Enable Status On Hit", "Enables inflicting status effects on hit.",
            WeaponUpgradeValueFormat.None,
            WeaponUpgradeTraits.None,
            null,
            (shooter, application) => shooter.applyStatusEffectOnHit = true));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.ShooterStatusEffectIndex,
            "{0} Affliction", "This weapon's hits now inflict {0}.",
            WeaponUpgradeValueFormat.StatusType,
            WeaponUpgradeTraits.Generated | WeaponUpgradeTraits.StatusRelated | WeaponUpgradeTraits.FirstOnHit,
            shooter => WeaponUpgradeRollUtility.RandomNegativeStatusEffectIndex(),
            (shooter, application) =>
            {
                shooter.EnableOnHitEffectByIndex((int)WeaponUpgradeRollUtility.ClampStatusType(application.Value));
                if (application.SeedStatusChance)
                    shooter.statusApplyChance = Mathf.Max(shooter.statusApplyChance, Mathf.Clamp01(application.SeededStatusChance));
            },
            shooter => shooter.applyStatusEffectOnHit));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterKnockbackFlat,
            "Crushing Force +{0}", "Hits knock enemies back with {0} additional force.",
            WeaponUpgradeValueFormat.Decimal1, 0.25f, 1.5f, false,
            (shooter, value) => shooter.knockbackForce += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterCullThreshold,
            "Culling Strike +{0}", "Enemies at or below {0} health are instantly slain by this weapon.",
            WeaponUpgradeValueFormat.Percent, 0.01f, 0.03f, false,
            (shooter, value) => shooter.cullThreshold = Mathf.Clamp01(shooter.cullThreshold + value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterChainHits,
            "Chaining Rounds +{0}", "Projectiles can seek {0} additional enemies after hitting a target.",
            WeaponUpgradeValueFormat.Integer, 1f, 1f, true,
            (shooter, value) => shooter.chainHits += Mathf.Max(1, Mathf.RoundToInt(value))));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterForkShotChance,
            "Forked Rounds +{0}", "Projectiles gain {0} chance to split into two weaker side shots.",
            WeaponUpgradeValueFormat.Percent, 0.08f, 0.2f, false,
            (shooter, value) => shooter.forkShotChance = Mathf.Clamp01(shooter.forkShotChance + value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.ShooterPenetrationFlat,
            "Piercing Rounds +{0}", "Projectiles can pass through {0} additional enemies before breaking.",
            WeaponUpgradeValueFormat.Integer, 1f, 1f, true,
            (shooter, value) => shooter.penetration += Mathf.Max(1, Mathf.RoundToInt(value))));
    }

    private static WeaponUpgradeDefinition Ranged(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        float min,
        float max,
        bool wholeNumbers,
        Action<SimpleShooter, float> apply,
        Predicate<SimpleShooter> eligibility = null,
        WeaponUpgradeTraits traits = Generated)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.Shooter,
            title,
            description,
            format,
            traits,
            application => Apply(application, apply),
            new WeaponUpgradeRange(min, max, wholeNumbers),
            eligibility: target => target.TryGetComponent(out SimpleShooter shooter) &&
                                   (eligibility == null || eligibility(shooter)),
            icon: target => target.TryGetComponent(out SimpleShooter shooter) ? shooter.weaponSprite : null);
    }

    private static WeaponUpgradeDefinition StatusRange(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        float min,
        float max,
        Action<SimpleShooter, float> apply,
        Predicate<SimpleShooter> extraEligibility = null)
    {
        return Ranged(
            type, title, description, format, min, max, false, apply,
            shooter => shooter.applyStatusEffectOnHit &&
                       (extraEligibility == null || extraEligibility(shooter)),
            Generated | WeaponUpgradeTraits.StatusRelated);
    }

    private static WeaponUpgradeDefinition Special(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        WeaponUpgradeTraits traits,
        Func<SimpleShooter, float> roll,
        Action<SimpleShooter, WeaponUpgradeApplication> apply,
        Predicate<SimpleShooter> eligibility = null)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.Shooter,
            title,
            description,
            format,
            traits,
            application =>
            {
                if (!application.Target.TryGetComponent(out SimpleShooter shooter)) return false;
                apply(shooter, application);
                shooter.UpdateStatsText();
                return true;
            },
            hasConfigurableRange: false,
            eligibility: target => target.TryGetComponent(out SimpleShooter shooter) &&
                                   (eligibility == null || eligibility(shooter)),
            specialRoll: roll == null
                ? null
                : target => target.TryGetComponent(out SimpleShooter shooter) ? roll(shooter) : 0f,
            icon: target => target.TryGetComponent(out SimpleShooter shooter) ? shooter.weaponSprite : null);
    }

    private static bool Apply(WeaponUpgradeApplication application, Action<SimpleShooter, float> apply)
    {
        if (!application.Target.TryGetComponent(out SimpleShooter shooter)) return false;
        apply(shooter, application.Value);
        shooter.UpdateStatsText();
        return true;
    }
}
