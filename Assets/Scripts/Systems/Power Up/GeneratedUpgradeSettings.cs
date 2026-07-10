using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneratedUpgradeSettings", menuName = "Power Ups/Generated Upgrade Settings")]
public class GeneratedUpgradeSettings : ScriptableObject
{
    public const string ResourceName = "GeneratedUpgradeSettings";
    public const float DefaultUpgradeWeight = 1f;
    public const float MinUpgradeWeight = 0f;
    public const float MaxUpgradeWeight = 10f;

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
    public class WeaponWeight
    {
        public WeaponUpgrades.UpgradeType type;
        [Range(MinUpgradeWeight, MaxUpgradeWeight)] public float weight = DefaultUpgradeWeight;
    }

    [Serializable]
    public class AccessoryWeight
    {
        public AccessoriesUpgrades.StatUpgradeType type;
        [Range(MinUpgradeWeight, MaxUpgradeWeight)] public float weight = DefaultUpgradeWeight;
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
    public List<WeaponWeight> weaponWeights = new();
    public List<AccessoryWeight> accessoryWeights = new();
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
        weaponRanges ??= new List<WeaponRange>();
        accessoryRanges ??= new List<AccessoryRange>();
        weaponWeights ??= new List<WeaponWeight>();
        accessoryWeights ??= new List<AccessoryWeight>();

        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type))
                continue;

            if (!weaponWeights.Exists(entry => entry != null && entry.type == type))
                weaponWeights.Add(new WeaponWeight { type = type, weight = DefaultUpgradeWeight });

            if (TryGetWeaponDefaults(type, out float min, out float max, out bool whole) &&
                !weaponRanges.Exists(entry => entry != null && entry.type == type))
            {
                weaponRanges.Add(new WeaponRange { type = type, min = min, max = max, wholeNumbers = whole });
            }
        }

        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None ||
                !TryGetAccessoryDefaults(type, out float min, out float max, out bool whole))
                continue;
            if (!accessoryRanges.Exists(entry => entry != null && entry.type == type))
                accessoryRanges.Add(new AccessoryRange { type = type, min = min, max = max, wholeNumbers = whole });
            if (!accessoryWeights.Exists(entry => entry != null && entry.type == type))
                accessoryWeights.Add(new AccessoryWeight { type = type, weight = DefaultUpgradeWeight });
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
        weaponRanges?.Find(entry => entry != null && entry.type == type);

    public AccessoryRange FindAccessoryRange(AccessoriesUpgrades.StatUpgradeType type) =>
        accessoryRanges?.Find(entry => entry != null && entry.type == type);

    public WeaponWeight FindWeaponWeight(WeaponUpgrades.UpgradeType type) =>
        weaponWeights?.Find(entry => entry != null && entry.type == type);

    public AccessoryWeight FindAccessoryWeight(AccessoriesUpgrades.StatUpgradeType type) =>
        accessoryWeights?.Find(entry => entry != null && entry.type == type);

    public float GetWeaponWeight(WeaponUpgrades.UpgradeType type) =>
        Mathf.Clamp(FindWeaponWeight(type)?.weight ?? DefaultUpgradeWeight, MinUpgradeWeight, MaxUpgradeWeight);

    public float GetAccessoryWeight(AccessoriesUpgrades.StatUpgradeType type) =>
        Mathf.Clamp(FindAccessoryWeight(type)?.weight ?? DefaultUpgradeWeight, MinUpgradeWeight, MaxUpgradeWeight);

    public static float GetConfiguredWeaponWeight(WeaponUpgrades.UpgradeType type)
    {
        GeneratedUpgradeSettings settings = Load();
        return settings != null ? settings.GetWeaponWeight(type) : DefaultUpgradeWeight;
    }

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

    public WeaponUpgradeDefinition PickWeaponUpgrade(IReadOnlyList<WeaponUpgradeDefinition> candidates)
    {
        int index = PickWeightedIndex(candidates, candidate => GetWeaponWeight(candidate.Type));
        return index >= 0 ? candidates[index] : null;
    }

    public AccessoriesUpgrades.StatUpgradeType PickAccessoryUpgrade(
        IReadOnlyList<AccessoriesUpgrades.StatUpgradeType> candidates)
    {
        int index = PickWeightedIndex(candidates, GetAccessoryWeight);
        return index >= 0 ? candidates[index] : AccessoriesUpgrades.StatUpgradeType.None;
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

    private static int PickWeightedIndex<T>(IReadOnlyList<T> candidates, Func<T, float> getWeight)
    {
        if (candidates == null || candidates.Count == 0)
            return -1;

        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
            total += Mathf.Max(0f, getWeight(candidates[i]));

        if (total <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        int lastPositiveIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            float weight = Mathf.Max(0f, getWeight(candidates[i]));
            if (weight <= 0f)
                continue;

            lastPositiveIndex = i;
            cumulative += weight;
            if (roll < cumulative)
                return i;
        }

        return lastPositiveIndex;
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
