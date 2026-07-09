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

    private static Sprite ResolveIcon(Transform target)
    {
        if (target.TryGetComponent(out Knife knife)) return knife.weaponSprite;
        if (target.TryGetComponent(out SimpleShooter shooter)) return shooter.weaponSprite;
        return null;
    }
}
