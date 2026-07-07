using UnityEngine;

public static class RarityRollRules
{
    public static Rarity NextRarity(Rarity r) => r switch
    {
        Rarity.Common => Rarity.Uncommon,
        Rarity.Uncommon => Rarity.Rare,
        Rarity.Rare => Rarity.Legendary,
        _ => Rarity.Legendary
    };

    public static int RollsFor(Rarity r) => r switch
    {
        Rarity.Common => 1,
        Rarity.Uncommon => 2,
        Rarity.Rare => 3,
        Rarity.Legendary => 4,
        _ => 1
    };

    public static int RollCountFor(Rarity rarity, System.Random rng)
    {
        return RollsFor(rarity);
    }
}
