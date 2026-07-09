using UnityEngine;

public sealed class FirstOnHitStatusOfferSource : IPowerUpOfferSource
{
    public void GenerateOffers(PowerUpOfferGenerationContext context)
    {
        if (context.Player == null) return;

        Knife[] knives = context.Player.GetComponentsInChildren<Knife>(true);
        for (int i = 0; i < knives.Length; i++)
        {
            Knife knife = knives[i];
            if (knife != null && knife.gameObject.activeInHierarchy && !knife.applyStatusEffectOnHit)
                CreateOffer(context, knife, WeaponUpgrades.UpgradeType.KnifeStatusEffectIndex, knife.weaponSprite);
        }

        SimpleShooter[] shooters = context.Player.GetComponentsInChildren<SimpleShooter>(true);
        for (int i = 0; i < shooters.Length; i++)
        {
            SimpleShooter shooter = shooters[i];
            if (shooter != null && shooter.gameObject.activeInHierarchy && !shooter.applyStatusEffectOnHit)
                CreateOffer(context, shooter, WeaponUpgrades.UpgradeType.ShooterStatusEffectIndex, shooter.weaponSprite);
        }
    }

    private static void CreateOffer(
        PowerUpOfferGenerationContext context,
        Component weapon,
        WeaponUpgrades.UpgradeType type,
        Sprite icon)
    {
        var eligibility = weapon.GetComponent<GeneratedUpgradeEligibility>();
        if (eligibility != null && !eligibility.allowGeneratedUpgrades)
            return;

        if (CountApplied(weapon.transform) >= WeaponUpgrades.MaxUpgrades)
            return;

        GameObject offerObject = context.CreateOfferObject(weapon.transform, "Generated On-Hit Status Upgrade");
        var upgrade = offerObject.AddComponent<WeaponUpgrades>();
        PowerUpRarity rarity = PowerUp.RollRandomRarity();
        int statusIndex = WeaponUpgradeRollUtility.RandomNegativeStatusEffectIndex();

        if (!upgrade.ConfigureAsOffer(type, statusIndex, rarity, applyRarityMultiplier: false))
        {
            Object.Destroy(offerObject);
            return;
        }

        upgrade.ConfigureStatusChanceSeed(context.FirstOnHitBaseChance);
        upgrade.Upgrade.powerUpIcon = icon;
        upgrade.RefreshPresentation();

        context.RegisterOffer(upgrade.Upgrade, offerObject);
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
