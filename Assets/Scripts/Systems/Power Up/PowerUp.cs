using System;
using NaughtyAttributes;
using UnityEngine;

public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Curse
}

[Flags]
public enum PowerUpTags
{
    None = 0,
    World = 1 << 0,
    Weapon = 1 << 1,
    Accessory = 1 << 2,
    Upgrade = 1 << 3
}

/// <summary>
/// Runtime offer shown by the power-up selector. The legacy serialized fields are
/// intentionally retained so existing scenes keep all of their data.
/// </summary>
[Serializable]
public class PowerUp
{
    public string powerUpName;
    [TextArea] public string powerUpDescription;

    [Header("Activation")]
    [Tooltip("Prefab or in-scene GameObject to spawn/enable when selected.")]
    public GameObject powerUpObject;

    [Header("Type")]
    [Tooltip("Treat this power-up as an Accessory (counts toward accessory cap).")]
    public bool IsAccessory;
    [Tooltip("Treat this power-up as a Weapon (counts toward weapon cap).")]
    public bool IsWeapon;
    [Tooltip("This entry upgrades an owned item and does not consume a weapon/accessory slot.")]
    public bool IsUpgrade;

    [Header("Visuals")]
    [Tooltip("Icon representing this power-up. If null, UI will use its default icon.")]
    [ShowAssetPreview] public Sprite powerUpIcon;

    [Header("Rarity")]
    [Tooltip("Multiplier tier for generated upgrade values.")]
    public PowerUpRarity rarity = PowerUpRarity.Common;

    [Header("Spawn Weight")]
    [Tooltip("Relative chance for this power-up to appear in selection. Higher = more common.")]
    [Min(0f)] public float weight = 1f;

    [SerializeField, HideInInspector] private PowerUpDefinition sourceDefinition;

    public PowerUpDefinition SourceDefinition => sourceDefinition;
    public float RarityMultiplier => GetRarityMultiplier(rarity);

    public PowerUpTags Tags
    {
        get
        {
            PowerUpTags tags = PowerUpTags.None;
            if (IsWeapon) tags |= PowerUpTags.Weapon;
            if (IsAccessory) tags |= PowerUpTags.Accessory;
            if (IsUpgrade) tags |= PowerUpTags.Upgrade;
            if ((tags & (PowerUpTags.Weapon | PowerUpTags.Accessory)) == 0)
                tags |= PowerUpTags.World;
            return tags;
        }
    }

    public static PowerUp FromDefinition(PowerUpDefinition definition)
    {
        if (definition == null) return null;

        PowerUpTags tags = definition.Tags;
        return new PowerUp
        {
            powerUpName = definition.DisplayName,
            powerUpDescription = definition.Description,
            powerUpObject = definition.ActivationObject,
            IsAccessory = (tags & PowerUpTags.Accessory) != 0,
            IsWeapon = (tags & PowerUpTags.Weapon) != 0,
            IsUpgrade = (tags & PowerUpTags.Upgrade) != 0,
            powerUpIcon = definition.Icon,
            rarity = definition.Rarity,
            weight = definition.SelectionWeight,
            sourceDefinition = definition
        };
    }

    public bool HasSameIdentity(PowerUp other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (sourceDefinition != null || other.sourceDefinition != null)
            return sourceDefinition != null && sourceDefinition == other.sourceDefinition;

        return powerUpObject != null &&
               powerUpObject == other.powerUpObject &&
               string.Equals(powerUpName, other.powerUpName, StringComparison.Ordinal);
    }

    public static PowerUpRarity RollRandomRarity() => GeneratedUpgradeSettings.RollPowerUpRarity();

    public static float GetRarityMultiplier(PowerUpRarity rarity) =>
        GeneratedUpgradeSettings.GetPowerUpRarityMultiplier(rarity);

    public static string GetRarityDisplayName(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => "Uncommon",
            PowerUpRarity.Rare => "Rare",
            PowerUpRarity.Curse => "Curse",
            _ => "Common",
        };
    }

    public static string GetRarityColor(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => "#33CC66",
            PowerUpRarity.Rare => "#4D8DFF",
            PowerUpRarity.Curse => "#B84DFF",
            _ => "#D9D9D9",
        };
    }
}
