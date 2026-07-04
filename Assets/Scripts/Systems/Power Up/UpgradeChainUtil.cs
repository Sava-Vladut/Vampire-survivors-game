using UnityEngine;

// Shared by WeaponUpgrades and AccessoriesUpgrades: both wire "the next upgrade
// in the chain" to the next sibling under the same parent that carries a matching
// component, and both push that next upgrade's PowerUp into the shared chooser
// exactly once. Previously each component reimplemented this identically.
public static class UpgradeChainUtil
{
    public static T FindNextSibling<T>(Transform self) where T : Component
    {
        if (self.parent == null) return null;

        var parent = self.parent;
        int myIndex = self.GetSiblingIndex();
        for (int i = myIndex + 1; i < parent.childCount; i++)
        {
            var candidate = parent.GetChild(i).GetComponent<T>();
            if (candidate != null) return candidate;
        }

        return null;
    }

    public static void EnqueueOnce(PowerUpChooser chooser, PowerUp powerUp)
    {
        if (chooser == null || powerUp == null) return;

        var list = chooser.powerUps;
        if (list != null && !list.Contains(powerUp))
            list.Add(powerUp);
    }

    // Scene-wide lookups are expensive to repeat once per upgrade instance (a weapon
    // can carry a dozen+ chained upgrades). Cache it; Unity's overridden null-check
    // treats a destroyed/unloaded instance as null, so a scene reload re-triggers the search.
    private static PowerUpChooser cachedChooser;

    public static PowerUpChooser GetChooser()
    {
        if (cachedChooser == null)
            cachedChooser = Object.FindAnyObjectByType<PowerUpChooser>();
        return cachedChooser;
    }
}
