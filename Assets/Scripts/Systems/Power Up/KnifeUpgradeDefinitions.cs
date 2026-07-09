using System;
using UnityEngine;

public static class KnifeUpgradeDefinitions
{
    private const WeaponUpgradeTraits Generated =
        WeaponUpgradeTraits.Generated | WeaponUpgradeTraits.ScalesWithRarity;
    private const float Epsilon = 0.0001f;

    public static void Register(WeaponUpgradeCatalogBuilder builder)
    {
        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeDamageFlat,
            "Tempered Power +{0}", "Each hit deals {0} more damage.",
            WeaponUpgradeValueFormat.Integer, 1f, 6f, true,
            (knife, value) =>
            {
                int amount = Mathf.RoundToInt(value);
                knife.minDamage += amount;
                knife.damage += amount;
            }));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeDamagePercent,
            "Weapon Mastery +{0}", "All damage from this weapon is increased by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.12f, false,
            (knife, value) =>
            {
                knife.minDamage = Mathf.RoundToInt(knife.minDamage * (1f + value));
                knife.damage = Mathf.RoundToInt(knife.damage * (1f + value));
            },
            knife => knife.damage > 0));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.KnifeDamageTypeIndex,
            "{0} Infusion", "Attunes every hit from this weapon to {0} damage.",
            WeaponUpgradeValueFormat.DamageType,
            WeaponUpgradeTraits.Generated,
            knife => WeaponUpgradeRollUtility.RollDifferentDamageType(knife.damageType),
            (knife, application) =>
                knife.damageType = WeaponUpgradeRollUtility.ResolveDifferentDamageType(knife.damageType, application.Value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeRadiusFlat,
            "Long Reach +{0}", "Strikes reach {0} farther from the weapon.",
            WeaponUpgradeValueFormat.Decimal2, 0.05f, 0.5f, false,
            (knife, value) => knife.radius += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeRadiusPercent,
            "Sweeping Reach +{0}", "Increases this weapon's attack radius by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.15f, false,
            (knife, value) => knife.radius *= 1f + value,
            knife => knife.radius > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeMaxTargetsFlat,
            "Cleave +{0}", "Each strike can hit {0} additional enemies.",
            WeaponUpgradeValueFormat.Integer, 1f, 2f, true,
            (knife, value) => knife.maxTargetsPerTick += Mathf.RoundToInt(value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeLifestealFlat,
            "Bloodthirst +{0}", "Converts an additional {0} of damage dealt into healing.",
            WeaponUpgradeValueFormat.Percent, 0.01f, 0.08f, false,
            (knife, value) => knife.lifestealPercent += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeLifestealPercent,
            "Greater Bloodthirst +{0}", "Increases this weapon's current lifesteal by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.2f, false,
            (knife, value) => knife.lifestealPercent *= 1f + value,
            knife => knife.lifestealPercent > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeCritChanceFlat,
            "Keen Edge +{0}", "Grants {0} additional critical-hit chance.",
            WeaponUpgradeValueFormat.Percent, 0.02f, 0.1f, false,
            (knife, value) => knife.critChance = Mathf.Clamp01(knife.critChance + value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeCritMultiplierFlat,
            "Savage Criticals +{0}", "Critical hits gain {0} additional damage multiplier.",
            WeaponUpgradeValueFormat.Multiplier, 0.05f, 0.3f, false,
            (knife, value) => knife.critMultiplier += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeSplashRadiusFlat,
            "Wider Impact +{0}", "Splash attacks reach {0} farther around the target.",
            WeaponUpgradeValueFormat.Decimal2, 0.1f, 0.75f, false,
            (knife, value) => knife.splashRadius += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeSplashRadiusPercent,
            "Expanding Impact +{0}", "Increases splash radius by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f, false,
            (knife, value) => knife.splashRadius *= 1f + value,
            knife => knife.splashRadius > Epsilon));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentFlat,
            "Aftershock +{0}", "Splash hits deal {0} more of the weapon's damage.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.15f, false,
            (knife, value) => knife.splashDamagePercent += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentPercent,
            "Greater Aftershock +{0}", "Increases current splash damage by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f, false,
            (knife, value) => knife.splashDamagePercent *= 1f + value,
            knife => knife.splashDamagePercent > Epsilon));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.KnifeStatusApplyChanceFlat,
            "Affliction Chance +{0}", "Hits are {0} more likely to inflict their on-hit effect.",
            WeaponUpgradeValueFormat.Percent, 0.03f, 0.15f,
            (knife, value) => knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance + value)));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.KnifeStatusApplyChancePercent,
            "Potent Affliction +{0}", "Multiplies this weapon's on-hit effect chance by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f,
            (knife, value) => knife.statusApplyChance = Mathf.Clamp01(knife.statusApplyChance * (1f + value)),
            knife => knife.statusApplyChance > Epsilon));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.KnifeStatusDurationFlat,
            "Lingering Curse +{0}", "On-hit effects remain on enemies for {0} longer.",
            WeaponUpgradeValueFormat.Seconds1, 0.25f, 1.5f,
            (knife, value) => knife.statusEffectDuration += value));

        builder.Add(StatusRange(
            WeaponUpgrades.UpgradeType.KnifeStatusDurationPercent,
            "Enduring Affliction +{0}", "Increases this weapon's on-hit effect duration by {0}.",
            WeaponUpgradeValueFormat.Percent, 0.05f, 0.25f,
            (knife, value) => knife.statusEffectDuration *= 1f + value,
            knife => knife.statusEffectDuration > Epsilon));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.KnifeEnableStatusEffect,
            "Enable Status On Hit", "Enables inflicting status effects on hit.",
            WeaponUpgradeValueFormat.None,
            WeaponUpgradeTraits.None,
            null,
            (knife, application) => knife.applyStatusEffectOnHit = true));

        builder.Add(Special(
            WeaponUpgrades.UpgradeType.KnifeStatusEffectIndex,
            "{0} Affliction", "This weapon's hits now inflict {0}.",
            WeaponUpgradeValueFormat.StatusType,
            WeaponUpgradeTraits.Generated | WeaponUpgradeTraits.StatusRelated | WeaponUpgradeTraits.FirstOnHit,
            knife => WeaponUpgradeRollUtility.RandomNegativeStatusEffectIndex(),
            (knife, application) =>
            {
                knife.EnableOnHitEffectByIndex((int)WeaponUpgradeRollUtility.ClampStatusType(application.Value));
                if (application.SeedStatusChance)
                    knife.statusApplyChance = Mathf.Max(knife.statusApplyChance, Mathf.Clamp01(application.SeededStatusChance));
            },
            knife => knife.applyStatusEffectOnHit));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeKnockbackFlat,
            "Crushing Force +{0}", "Hits knock enemies back with {0} additional force.",
            WeaponUpgradeValueFormat.Decimal1, 0.25f, 1.5f, false,
            (knife, value) => knife.knockbackForce += value));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeCullThreshold,
            "Culling Strike +{0}", "Enemies at or below {0} health are instantly slain by this weapon.",
            WeaponUpgradeValueFormat.Percent, 0.01f, 0.03f, false,
            (knife, value) => knife.cullThreshold = Mathf.Clamp01(knife.cullThreshold + value)));

        builder.Add(Ranged(
            WeaponUpgrades.UpgradeType.KnifeEchoStrikeChance,
            "Echo Edge +{0}", "Hits gain {0} chance to repeat for half damage.",
            WeaponUpgradeValueFormat.Percent, 0.08f, 0.2f, false,
            (knife, value) => knife.echoStrikeChance = Mathf.Clamp01(knife.echoStrikeChance + value)));
    }

    private static WeaponUpgradeDefinition Ranged(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        float min,
        float max,
        bool wholeNumbers,
        Action<Knife, float> apply,
        Predicate<Knife> eligibility = null,
        WeaponUpgradeTraits traits = Generated)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.Knife,
            title,
            description,
            format,
            traits,
            application => Apply(application, apply),
            new WeaponUpgradeRange(min, max, wholeNumbers),
            eligibility: target => target.TryGetComponent(out Knife knife) &&
                                   (eligibility == null || eligibility(knife)),
            icon: target => target.TryGetComponent(out Knife knife) ? knife.weaponSprite : null);
    }

    private static WeaponUpgradeDefinition StatusRange(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        float min,
        float max,
        Action<Knife, float> apply,
        Predicate<Knife> extraEligibility = null)
    {
        return Ranged(
            type, title, description, format, min, max, false, apply,
            knife => knife.applyStatusEffectOnHit &&
                     (extraEligibility == null || extraEligibility(knife)),
            Generated | WeaponUpgradeTraits.StatusRelated);
    }

    private static WeaponUpgradeDefinition Special(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        WeaponUpgradeTraits traits,
        Func<Knife, float> roll,
        Action<Knife, WeaponUpgradeApplication> apply,
        Predicate<Knife> eligibility = null)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.Knife,
            title,
            description,
            format,
            traits,
            application =>
            {
                if (!application.Target.TryGetComponent(out Knife knife)) return false;
                apply(knife, application);
                knife.UpdateStatsText();
                return true;
            },
            hasConfigurableRange: false,
            eligibility: target => target.TryGetComponent(out Knife knife) &&
                                   (eligibility == null || eligibility(knife)),
            specialRoll: roll == null
                ? null
                : target => target.TryGetComponent(out Knife knife) ? roll(knife) : 0f,
            icon: target => target.TryGetComponent(out Knife knife) ? knife.weaponSprite : null);
    }

    private static bool Apply(WeaponUpgradeApplication application, Action<Knife, float> apply)
    {
        if (!application.Target.TryGetComponent(out Knife knife)) return false;
        apply(knife, application.Value);
        knife.UpdateStatsText();
        return true;
    }
}
