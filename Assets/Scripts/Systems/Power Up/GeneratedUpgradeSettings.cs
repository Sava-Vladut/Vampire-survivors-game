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
            PowerUpRarity.Curse => 0.05f,
            _ => 1f,
        };
    }

    public static float GetDefaultRarityMultiplier(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => 1.5f,
            PowerUpRarity.Rare => 2f,
            PowerUpRarity.Curse => 3f,
            _ => 1f,
        };
    }

    public static bool TryGetDefaultWeaponRange(WeaponUpgrades.UpgradeType type, out float min, out float max, out bool wholeNumbers) =>
        TryGetWeaponDefaults(type, out min, out max, out wholeNumbers);

    public static bool TryGetDefaultAccessoryRange(AccessoriesUpgrades.StatUpgradeType type, out float min, out float max, out bool wholeNumbers) =>
        TryGetAccessoryDefaults(type, out min, out max, out wholeNumbers);

    private static PowerUpRarity RollDefaultRarity()
    {
        float total = 0f;
        foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
            total += Mathf.Max(0f, GetDefaultRarityFrequency(rarity));

        float roll = UnityEngine.Random.value * total;
        foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
        {
            roll -= Mathf.Max(0f, GetDefaultRarityFrequency(rarity));
            if (roll <= 0f) return rarity;
        }

        return PowerUpRarity.Common;
    }

    private static bool TryGetWeaponDefaults(WeaponUpgrades.UpgradeType type, out float min, out float max, out bool whole)
    {
        return WeaponUpgradeCatalog.TryGetDefaultRange(type, out min, out max, out whole);
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
            case AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat: min = 1f; max = 1f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.CooldownReduction: min = 0.03f; max = 0.12f; return true;
            case AccessoriesUpgrades.StatUpgradeType.AttackSpeedPercent: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.CriticalChanceFlat: min = 0.02f; max = 0.10f; return true;
            case AccessoriesUpgrades.StatUpgradeType.CriticalDamageFlat: min = 0.10f; max = 0.50f; return true;
            case AccessoriesUpgrades.StatUpgradeType.WeaponAreaPercent: min = 0.05f; max = 0.25f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ProjectileSpeedPercent: min = 0.05f; max = 0.30f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ProjectileLifetimePercent: min = 0.05f; max = 0.30f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ProjectilePenetrationFlat: min = 1f; max = 1f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.KnockbackStrengthFlat: min = 0.50f; max = 2.50f; return true;
            case AccessoriesUpgrades.StatUpgradeType.PickupRadiusFlat: min = 0.50f; max = 3f; return true;
            case AccessoriesUpgrades.StatUpgradeType.XpGainPercent: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.HealingReceivedPercent: min = 0.05f; max = 0.25f; return true;
            case AccessoriesUpgrades.StatUpgradeType.StatusDurationPercent: min = 0.10f; max = 0.40f; return true;
            case AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat: min = 0.03f; max = 0.12f; return true;
            case AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.AdditionalDashChargeFlat: min = 1f; max = 1f; whole = true; return true;
            case AccessoriesUpgrades.StatUpgradeType.DashInvulnerabilityFlat: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.ContactDamageReduction: min = 0.05f; max = 0.20f; return true;
            case AccessoriesUpgrades.StatUpgradeType.EnemySlowAura: min = 0.05f; max = 0.20f; return true;
            default: return false;
        }
    }
}
