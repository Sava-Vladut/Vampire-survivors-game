using System;
using UnityEngine;

public static class WeaponTickUpgradeDefinitions
{
    private const WeaponUpgradeTraits Generated =
        WeaponUpgradeTraits.Generated | WeaponUpgradeTraits.ScalesWithRarity;

    public static void Register(WeaponUpgradeCatalogBuilder builder)
    {
        builder.Add(Create(
            WeaponUpgrades.UpgradeType.TickRateFlat,
            "Quickened Strikes -{0}", "Attacks trigger {0} sooner each cycle.",
            WeaponUpgradeValueFormat.Seconds2,
            new WeaponUpgradeRange(0.03f, 0.25f),
            (tick, value) => tick.interval = Mathf.Max(0.05f, tick.interval - value)));

        builder.Add(Create(
            WeaponUpgrades.UpgradeType.TickRatePercent,
            "Battle Rhythm +{0}", "Reduces the delay between attacks by {0}.",
            WeaponUpgradeValueFormat.Percent,
            new WeaponUpgradeRange(0.03f, 0.15f),
            (tick, value) => tick.interval = Mathf.Max(0.05f, tick.interval * (1f - value)),
            tick => tick.interval > 0.0001f));

        builder.Add(CreateGlobalMana(
            WeaponUpgrades.UpgradeType.ManaMaxFlat,
            "Arcane Reservoir +{0}",
            "Increases the player's global maximum mana by {0}.",
            WeaponUpgradeValueFormat.Integer,
            new WeaponUpgradeRange(10f, 30f, true),
            (mana, value) => mana.IncreaseMaxMana(Mathf.Max(1, Mathf.RoundToInt(value)))));

        builder.Add(CreateGlobalMana(
            WeaponUpgrades.UpgradeType.ManaRegenerationFlat,
            "Arcane Flow +{0}",
            "Increases the player's global mana regeneration by {0} per second.",
            WeaponUpgradeValueFormat.Decimal1,
            new WeaponUpgradeRange(1f, 4f),
            (mana, value) => mana.RegenerationPerSecond += Mathf.Max(0f, value)));
    }

    private static WeaponUpgradeDefinition Create(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        WeaponUpgradeRange range,
        Action<WeaponTick, float> apply,
        Predicate<WeaponTick> eligibility = null)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.WeaponTick,
            title,
            description,
            format,
            Generated,
            application =>
            {
                if (!application.Target.TryGetComponent(out WeaponTick tick)) return false;
                apply(tick, application.Value);
                return true;
            },
            range,
            eligibility: target => target.TryGetComponent(out WeaponTick tick) &&
                                   (eligibility == null || eligibility(tick)),
            icon: ResolveIcon);
    }

    private static WeaponUpgradeDefinition CreateGlobalMana(
        WeaponUpgrades.UpgradeType type,
        string title,
        string description,
        WeaponUpgradeValueFormat format,
        WeaponUpgradeRange range,
        Action<PlayerMana, float> apply)
    {
        return new WeaponUpgradeDefinition(
            type,
            WeaponUpgradeTarget.WeaponTick,
            title,
            description,
            format,
            Generated,
            application =>
            {
                if (!application.Target.TryGetComponent(out WeaponTick tick) ||
                    !tick.AllowsGlobalManaDrops)
                {
                    return false;
                }

                PlayerMana mana = PlayerMana.Find(application.Target);
                if (mana == null) return false;

                apply(mana, application.Value);
                return true;
            },
            range,
            eligibility: target =>
                target.TryGetComponent(out WeaponTick tick) &&
                tick.AllowsGlobalManaDrops &&
                PlayerMana.Find(target) != null,
            icon: ResolveIcon);
    }

    private static Sprite ResolveIcon(Transform target)
    {
        if (target.TryGetComponent(out Knife knife)) return knife.weaponSprite;
        if (target.TryGetComponent(out SimpleShooter shooter)) return shooter.weaponSprite;
        return null;
    }
}
