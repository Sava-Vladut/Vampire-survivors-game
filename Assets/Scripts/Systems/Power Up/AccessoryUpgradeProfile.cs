using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AccessoryUpgradeProfile", menuName = "Power Ups/Accessory Upgrade Profile")]
public sealed class AccessoryUpgradeProfile : ScriptableObject
{
    [Min(0), SerializeField] private int maxUpgrades = AccessoriesUpgrades.MaxUpgrades;
    [SerializeField] private AccessoriesUpgrades.StatUpgradeType[] allowedTypes = Array.Empty<AccessoriesUpgrades.StatUpgradeType>();

    public int MaxUpgrades => Mathf.Max(0, maxUpgrades);
    public IReadOnlyList<AccessoriesUpgrades.StatUpgradeType> AllowedTypes => allowedTypes;

    public bool Allows(AccessoriesUpgrades.StatUpgradeType type)
    {
        if (allowedTypes == null || allowedTypes.Length == 0) return IsDefaultType(type);
        return Array.IndexOf(allowedTypes, type) >= 0;
    }

    public static bool IsDefaultType(AccessoriesUpgrades.StatUpgradeType type) =>
        type != AccessoriesUpgrades.StatUpgradeType.None &&
        type != AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat &&
        type != AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat &&
        type != AccessoriesUpgrades.StatUpgradeType.ThornsFlat &&
        type != AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat;
}
