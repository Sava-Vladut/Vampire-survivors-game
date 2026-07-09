using System.Collections.Generic;

/// <summary>
/// Central read-only registry assembled from target-specific definition modules.
/// Adding a new weapon family does not require editing WeaponUpgrades behavior.
/// </summary>
public static class WeaponUpgradeCatalog
{
    private static readonly Dictionary<WeaponUpgrades.UpgradeType, WeaponUpgradeDefinition> ByType;
    private static readonly List<WeaponUpgradeDefinition> Ordered;

    static WeaponUpgradeCatalog()
    {
        ByType = new Dictionary<WeaponUpgrades.UpgradeType, WeaponUpgradeDefinition>();
        Ordered = new List<WeaponUpgradeDefinition>();
        var builder = new WeaponUpgradeCatalogBuilder(ByType, Ordered);
        KnifeUpgradeDefinitions.Register(builder);
        ShooterUpgradeDefinitions.Register(builder);
        WeaponTickUpgradeDefinitions.Register(builder);
    }

    public static IReadOnlyList<WeaponUpgradeDefinition> All => Ordered;

    public static bool TryGet(WeaponUpgrades.UpgradeType type, out WeaponUpgradeDefinition definition) =>
        ByType.TryGetValue(type, out definition);

    public static bool IsGenerated(WeaponUpgrades.UpgradeType type) =>
        TryGet(type, out WeaponUpgradeDefinition definition) && definition.IsGenerated;

    public static bool TryGetDefaultRange(
        WeaponUpgrades.UpgradeType type,
        out float min,
        out float max,
        out bool wholeNumbers)
    {
        if (TryGet(type, out WeaponUpgradeDefinition definition) && definition.HasConfigurableRange)
        {
            min = definition.DefaultRange.Min;
            max = definition.DefaultRange.Max;
            wholeNumbers = definition.DefaultRange.WholeNumbers;
            return true;
        }

        min = max = 0f;
        wholeNumbers = false;
        return false;
    }
}

public sealed class WeaponUpgradeCatalogBuilder
{
    private readonly Dictionary<WeaponUpgrades.UpgradeType, WeaponUpgradeDefinition> byType;
    private readonly List<WeaponUpgradeDefinition> ordered;

    internal WeaponUpgradeCatalogBuilder(
        Dictionary<WeaponUpgrades.UpgradeType, WeaponUpgradeDefinition> byType,
        List<WeaponUpgradeDefinition> ordered)
    {
        this.byType = byType;
        this.ordered = ordered;
    }

    public void Add(WeaponUpgradeDefinition definition)
    {
        if (definition == null || byType.ContainsKey(definition.Type))
            return;

        byType.Add(definition.Type, definition);
        ordered.Add(definition);
    }
}
