using System;
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

    // Accessory upgrades apply directly to the player's singleton components rather
    // than a transform.parent-scoped weapon, so they're cached the same way.
    private static SimpleHealth cachedHealth;

    public static SimpleHealth GetHealth()
    {
        if (cachedHealth == null)
            cachedHealth = Object.FindAnyObjectByType<SimpleHealth>();
        return cachedHealth;
    }

    private static Snappy2DController cachedController;

    public static Snappy2DController GetController()
    {
        if (cachedController == null)
            cachedController = Object.FindAnyObjectByType<Snappy2DController>();
        return cachedController;
    }

    // ===== Shared tiered-regeneration tuning (WeaponUpgrades/AccessoriesUpgrades) =====
    // When the generated tail rolls a stat type the weapon/accessory already owns, it
    // levels that stat up (bigger roll) instead of adding an unrelated fresh one. This
    // is deliberately rarer than getting something new, so it stays a special moment.
    public const float LevelUpChance = 0.2f;
    public const int MaxUpgradeTier = 5;

    public static float TierMultiplier(int tier) => 1f + 0.5f * (tier - 1);

    // Shared by both WeaponUpgrades.CountExistingTier and AccessoriesUpgrades.CountExistingTier:
    // counts existing siblings (authored or generated) whose upgradeType matches, regardless
    // of position - each file supplies its own predicate since their UpgradeType enums differ.
    public static int CountSiblingsMatching<T>(Transform parent, Func<T, bool> predicate) where T : Component
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i).GetComponent<T>();
            if (c != null && predicate(c)) count++;
        }
        return count;
    }

    public static string TierRoman(int tier) => tier switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => "V",
    };
}
