using UnityEngine;

public static class WeaponRerollTargetDisplay
{
    public static string GetExtraText(WeaponRarityController controller)
    {
        if (!controller) return string.Empty;

        var shooter = controller.GetComponent<SimpleShooter>();
        if (shooter != null)
        {
            shooter.UpdateStatsText();
            return shooter.statsTextInstance != null ? shooter.statsTextInstance.text : string.Empty;
        }

        var knife = controller.GetComponent<Knife>();
        if (knife != null)
        {
            knife.UpdateStatsText();
            return knife.statsTextInstance != null ? knife.statsTextInstance.text : string.Empty;
        }

        var accessory = controller.GetComponent<Accessory>();
        if (accessory != null)
        {
            accessory.NotifyRootToRefresh();
            return accessory.statsTextInstance != null ? accessory.statsTextInstance.text : string.Empty;
        }

        return string.Empty;
    }

    public static Sprite GetSprite(WeaponRarityController controller)
    {
        if (!controller) return null;

        var shooter = controller.GetComponent<SimpleShooter>();
        if (shooter != null) return shooter.weaponSprite;

        var knife = controller.GetComponent<Knife>();
        if (knife != null) return knife.weaponSprite;

        var accessory = controller.GetComponent<Accessory>();
        if (accessory != null) return accessory.icon;

        return null;
    }
}
