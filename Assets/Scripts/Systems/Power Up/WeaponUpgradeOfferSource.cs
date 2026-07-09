using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponUpgradeOfferSource : IPowerUpOfferSource
{
    public void GenerateOffers(PowerUpOfferGenerationContext context)
    {
        if (context.Player == null) return;

        var targets = new HashSet<Transform>();
        AddTargets(context.Player.GetComponentsInChildren<Knife>(true), targets);
        AddTargets(context.Player.GetComponentsInChildren<SimpleShooter>(true), targets);
        AddTargets(context.Player.GetComponentsInChildren<WeaponTick>(true), targets);

        foreach (Transform target in targets)
        {
            if (target == null || !target.gameObject.activeInHierarchy || !IsEligible(target))
                continue;

            int applied = CountApplied(target);
            int offerCount = Mathf.Min(context.OffersPerWeapon, WeaponUpgrades.MaxUpgrades - applied);
            if (offerCount <= 0) continue;

            var usedTypes = new HashSet<WeaponUpgrades.UpgradeType>();
            for (int i = 0; i < offerCount; i++)
            {
                GameObject offerObject = context.CreateOfferObject(target, "Generated Weapon Upgrade");
                var upgrade = offerObject.AddComponent<WeaponUpgrades>();
                if (!upgrade.RandomizeAsOffer(usedTypes))
                {
                    Object.Destroy(offerObject);
                    break;
                }

                usedTypes.Add(upgrade.upgradeType);
                context.RegisterOffer(upgrade.Upgrade, offerObject);
            }
        }
    }

    private static void AddTargets<T>(T[] components, HashSet<Transform> targets) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null)
                targets.Add(components[i].transform);
    }

    private static bool IsEligible(Transform target)
    {
        var eligibility = target.GetComponent<GeneratedUpgradeEligibility>();
        return eligibility == null || eligibility.allowGeneratedUpgrades;
    }

    private static int CountApplied(Transform target)
    {
        int count = 0;
        WeaponUpgrades[] upgrades = target.GetComponentsInChildren<WeaponUpgrades>(true);
        for (int i = 0; i < upgrades.Length; i++)
            if (upgrades[i] != null && upgrades[i].HasApplied)
                count++;
        return count;
    }
}
