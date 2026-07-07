using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneratedUpgradeSettings", menuName = "Power Ups/Generated Upgrade Settings")]
public class GeneratedUpgradeSettings : ScriptableObject
{
    public const string ResourceName = "GeneratedUpgradeSettings";

    [Serializable]
    public class WeaponRange
    {
        public WeaponUpgrades.UpgradeType type;
        public float min;
        public float max;
        public bool wholeNumbers;
    }

    [Serializable]
    public class AccessoryRange
    {
        public AccessoriesUpgrades.StatUpgradeType type;
        public float min;
        public float max;
        public bool wholeNumbers;
    }

    [Serializable]
    public class PowerUpRaritySetting
    {
        public PowerUpRarity rarity;
        [Min(0f)] public float frequency = 1f;
        [Min(0f)] public float strengthMultiplier = 1f;
    }

    public List<WeaponRange> weaponRanges = new();
    public List<AccessoryRange> accessoryRanges = new();
    public List<PowerUpRaritySetting> raritySettings = new();

    private static GeneratedUpgradeSettings cached;

    public static GeneratedUpgradeSettings Load()
    {
        if (cached == null)
            cached = Resources.Load<GeneratedUpgradeSettings>(ResourceName);
        return cached;
    }

    public void EnsureAllRanges()
    {
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type) || !TryGetWeaponDefaults(type, out float min, out float max, out bool whole))
                continue;
            if (weaponRanges.Exists(entry => entry.type == type))
                continue;
            weaponRanges.Add(new WeaponRange { type = type, min = min, max = max, wholeNumbers = whole });
        }

        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None ||
                !TryGetAccessoryDefaults(type, out float min, out float max, out bool whole))
                continue;
            if (accessoryRanges.Exists(entry => entry.type == type))
                continue;
            accessoryRanges.Add(new AccessoryRange { type = type, min = min, max = max, wholeNumbers = whole });
        }

        EnsureAllRarities();
    }

    private void EnsureAllRarities()
    {
        raritySettings ??= new List<PowerUpRaritySetting>();

        foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
        {
            if (raritySettings.Exists(entry => entry.rarity == rarity))
                continue;

            raritySettings.Add(new PowerUpRaritySetting
            {
                rarity = rarity,
                frequency = GetDefaultRarityFrequency(rarity),
                strengthMultiplier = GetDefaultRarityMultiplier(rarity)
            });
        }
    }

    public WeaponRange FindWeaponRange(WeaponUpgrades.UpgradeType type) =>
        weaponRanges.Find(entry => entry.type == type);

    public AccessoryRange FindAccessoryRange(AccessoriesUpgrades.StatUpgradeType type) =>
        accessoryRanges.Find(entry => entry.type == type);

    public PowerUpRaritySetting FindRaritySetting(PowerUpRarity rarity) =>
        raritySettings?.Find(entry => entry.rarity == rarity);

    public static bool TryRollWeapon(WeaponUpgrades.UpgradeType type, out float value)
    {
        var settings = Load();
        var range = settings != null ? settings.FindWeaponRange(type) : null;
        if (range == null)
        {
            value = 0f;
            return false;
        }

        value = Roll(range.min, range.max, range.wholeNumbers);
        return true;
    }

    public static PowerUpRarity RollPowerUpRarity()
    {
        var settings = Load();
        var raritySettings = settings != null ? settings.raritySettings : null;
        if (raritySettings == null || raritySettings.Count == 0)
            return RollDefaultRarity();

        float total = 0f;
        foreach (var entry in raritySettings)
            if (entry != null)
                total += Mathf.Max(0f, entry.frequency);

        if (total <= 0f)
            return PowerUpRarity.Common;

        float roll = UnityEngine.Random.value * total;
        foreach (var entry in raritySettings)
        {
            if (entry == null) continue;
            roll -= Mathf.Max(0f, entry.frequency);
            if (roll <= 0f)
                return entry.rarity;
        }

        return PowerUpRarity.Common;
    }

    public static float GetPowerUpRarityMultiplier(PowerUpRarity rarity)
    {
        var settings = Load();
        var entry = settings != null ? settings.FindRaritySetting(rarity) : null;
        return entry != null ? Mathf.Max(0f, entry.strengthMultiplier) : GetDefaultRarityMultiplier(rarity);
    }

    public static bool TryRollAccessory(AccessoriesUpgrades.StatUpgradeType type, out float value)
    {
        var settings = Load();
        var range = settings != null ? settings.FindAccessoryRange(type) : null;
        if (range == null)
        {
            value = 0f;
            return false;
        }

        value = Roll(range.min, range.max, range.wholeNumbers);
        return true;
    }

    private static float Roll(float a, float b, bool wholeNumbers)
    {
        float min = Mathf.Min(a, b);
        float max = Mathf.Max(a, b);
        float rolled = Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        return wholeNumbers ? Mathf.Round(rolled) : rolled;
    }

    public static float GetDefaultRarityFrequency(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => 1f,
            PowerUpRarity.Rare => 1f,
            _ => 1f,
        };
    }

    public static float GetDefaultRarityMultiplier(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => 1.5f,
            PowerUpRarity.Rare => 2f,
            _ => 1f,
        };
    }

    private static PowerUpRarity RollDefaultRarity()
    {
        return (PowerUpRarity)UnityEngine.Random.Range(0, 3);
    }

    private static bool TryGetWeaponDefaults(WeaponUpgrades.UpgradeType type, out float min, out float max, out bool whole)
    {
        min = max = 0f;
        whole = false;
        string name = type.ToString();

        if (name.Contains("DamageFlat")) { min = 1f; max = 6f; whole = true; return true; }
        if (name.Contains("DamagePercent")) { min = 0.03f; max = 0.12f; return true; }
        if (name.Contains("CritChance")) { min = 0.02f; max = 0.10f; return true; }
        if (name.Contains("CritMultiplier")) { min = 0.05f; max = 0.30f; return true; }
        if (name.Contains("StatusApplyChanceFlat")) { min = 0.03f; max = 0.15f; return true; }
        if (name.Contains("StatusApplyChancePercent")) { min = 0.05f; max = 0.25f; return true; }
        if (name.Contains("StatusDurationFlat")) { min = 0.25f; max = 1.5f; return true; }
        if (name.Contains("StatusDurationPercent")) { min = 0.05f; max = 0.25f; return true; }
        if (name.Contains("Knockback")) { min = 0.25f; max = 1.5f; return true; }
        if (name.Contains("CullThreshold")) { min = 0.01f; max = 0.03f; return true; }
        if (name.Contains("PenetrationFlat")) { min = max = 1f; whole = true; return true; }
        if (name.Contains("MaxTargets") || name.Contains("ProjectileCount") || name.Contains("BurstCountFlat"))
        { min = 1f; max = 2f; whole = true; return true; }

        switch (type)
        {
            case WeaponUpgrades.UpgradeType.KnifeRadiusFlat: min = 0.05f; max = 0.50f; return true;
            case WeaponUpgrades.UpgradeType.KnifeRadiusPercent: min = 0.03f; max = 0.15f; return true;
            case WeaponUpgrades.UpgradeType.KnifeLifestealFlat: min = 0.01f; max = 0.08f; return true;
            case WeaponUpgrades.UpgradeType.KnifeLifestealPercent: min = 0.05f; max = 0.20f; return true;
            case WeaponUpgrades.UpgradeType.KnifeSplashRadiusFlat: min = 0.10f; max = 0.75f; return true;
            case WeaponUpgrades.UpgradeType.KnifeSplashRadiusPercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentFlat: min = 0.03f; max = 0.15f; return true;
            case WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentPercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.ShooterSpreadAngleFlat: min = 1f; max = 10f; return true;
            case WeaponUpgrades.UpgradeType.ShooterSpreadAnglePercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.ShooterProjectileSpeedFlat: min = 0.25f; max = 2.5f; return true;
            case WeaponUpgrades.UpgradeType.ShooterProjectileSpeedPercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.ShooterLifetimeFlat: min = 0.15f; max = 1f; return true;
            case WeaponUpgrades.UpgradeType.ShooterLifetimePercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.TickRateFlat: min = 0.03f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.TickRatePercent: min = 0.03f; max = 0.15f; return true;
            case WeaponUpgrades.UpgradeType.BurstCountPercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.BurstSpacingFlat: min = 0.01f; max = 0.15f; return true;
            case WeaponUpgrades.UpgradeType.BurstSpacingPercent: min = 0.05f; max = 0.25f; return true;
            case WeaponUpgrades.UpgradeType.KnifeEchoStrikeChance: min = 0.08f; max = 0.20f; return true;
            case WeaponUpgrades.UpgradeType.ShooterForkShotChance: min = 0.08f; max = 0.20f; return true;
            default: return false;
        }
    }

    private static bool TryGetAccessoryDefaults(AccessoriesUpgrades.StatUpgradeType type, out float min, out float max, out bool whole)
    {
        min = max = 0f;
        whole = false;
        switch (type)
        {
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat: min = 15f; max = 60f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthPercent: min = 0.05f; max = 0.25f; return true;
            case AccessoriesUpgrades.StatUpgradeType.RegenFlat: min = 0.10f; max = 1.50f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ArmorFlat: min = 1f; max = 6f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.ArmorPercent: min = 0.05f; max = 0.25f; return true;
            case AccessoriesUpgrades.StatUpgradeType.EvasionFlat: min = 2f; max = 12f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.EvasionPercent: min = 0.05f; max = 0.25f; return true;
            case AccessoriesUpgrades.StatUpgradeType.FireResist:
            case AccessoriesUpgrades.StatUpgradeType.ColdResist:
            case AccessoriesUpgrades.StatUpgradeType.LightningResist:
            case AccessoriesUpgrades.StatUpgradeType.PoisonResist: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat: min = 0.15f; max = 0.75f; return true;
            case AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat: min = 0.25f; max = 1.50f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ThornsFlat: min = 2f; max = 12f; whole = true; return true;
            default: return false;
        }
    }
}
