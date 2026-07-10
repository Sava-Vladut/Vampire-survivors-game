using System.Collections.Generic;
using UnityEngine;

public sealed class AccessoryUpgradeOfferSource : IPowerUpOfferSource
{
    public void GenerateOffers(PowerUpOfferGenerationContext context)
    {
        AccessoryInventory inventory = context.Player != null
            ? context.Player.GetComponentInChildren<AccessoryInventory>(true)
            : null;
        if (inventory == null) return;

        IReadOnlyList<Accessory> accessories = inventory.EquippedAccessories;
        for (int i = 0; i < accessories.Count; i++)
        {
            Accessory accessory = accessories[i];
            if (accessory == null || !accessory.gameObject.activeInHierarchy || !IsEligible(accessory))
                continue;

            int applied = CountApplied(accessory.transform);
            int offerCount = Mathf.Min(context.OffersPerAccessory, accessory.MaxUpgrades - applied);
            if (offerCount <= 0) continue;

            var usedTypes = new HashSet<AccessoriesUpgrades.StatUpgradeType>();
            for (int offerIndex = 0; offerIndex < offerCount; offerIndex++)
            {
                GameObject offerObject = context.CreateOfferObject(accessory.transform, "Generated Accessory Upgrade");
                var upgrade = offerObject.AddComponent<AccessoriesUpgrades>();
                if (!upgrade.RandomizeAsOffer(context.Chooser, usedTypes))
                {
                    Object.Destroy(offerObject);
                    break;
                }

                usedTypes.Add(upgrade.upgradeType);
                context.RegisterOffer(upgrade.Upgrade, offerObject);
            }
        }
    }

    private static bool IsEligible(Accessory accessory)
    {
        var eligibility = accessory.GetComponent<GeneratedUpgradeEligibility>();
        return eligibility == null || eligibility.allowGeneratedUpgrades;
    }

    private static int CountApplied(Transform target)
    {
        int count = 0;
        AccessoriesUpgrades[] upgrades = target.GetComponentsInChildren<AccessoriesUpgrades>(true);
        for (int i = 0; i < upgrades.Length; i++)
            if (upgrades[i] != null && upgrades[i].HasApplied)
                count++;
        return count;
    }
}
