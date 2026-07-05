using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates fresh random upgrade offers for owned weapons and accessories each
/// time the power-up selection opens (PowerUpSelectionUI calls RefreshOffers).
/// Each offer lives as an inactive child GameObject under its target with a
/// configured WeaponUpgrades/AccessoriesUpgrades component; picking the offer
/// activates that object, which applies the upgrade in Awake. Unpicked offers
/// are destroyed when the panel closes (ClearOffers).
/// Each weapon/accessory can receive at most WeaponUpgrades.MaxUpgrades /
/// AccessoriesUpgrades.MaxUpgrades (20) applied upgrades.
/// </summary>
[DisallowMultipleComponent]
public class RandomUpgradeGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Chooser whose available pool receives the generated offers. Auto-found if left empty.")]
    [SerializeField] private PowerUpChooser powerUpChooser;

    [Header("Offers")]
    [Tooltip("How many fresh upgrade offers to roll per owned weapon each time the selection opens.")]
    [Min(1)][SerializeField] private int offersPerWeapon = 3;
    [Tooltip("How many fresh upgrade offers to roll per owned accessory each time the selection opens.")]
    [Min(1)][SerializeField] private int offersPerAccessory = 2;

    private readonly List<PowerUp> activeOffers = new();

    private void Awake()
    {
        if (powerUpChooser == null)
            powerUpChooser = GetComponent<PowerUpChooser>();
        if (powerUpChooser == null)
            powerUpChooser = FindAnyObjectByType<PowerUpChooser>();
    }

    /// <summary>Discards any previous offers and rolls new ones for every eligible target.</summary>
    public void RefreshOffers()
    {
        ClearOffers();

        if (powerUpChooser == null || powerUpChooser.powerUps == null)
        {
            Debug.LogWarning("[RandomUpgradeGenerator] No PowerUpChooser available; cannot generate offers.", this);
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            GenerateWeaponOffers(player.transform);
        else
            Debug.LogWarning("[RandomUpgradeGenerator] No GameObject tagged 'Player'; skipping weapon offers.", this);

        GenerateAccessoryOffers();
    }

    /// <summary>
    /// Removes offers that were not picked from the chooser pool and destroys their
    /// objects. Picked offers (already moved to selectedPowerUps) are left applied.
    /// </summary>
    public void ClearOffers()
    {
        foreach (var offer in activeOffers)
        {
            if (powerUpChooser != null && powerUpChooser.powerUps != null &&
                powerUpChooser.powerUps.Remove(offer))
            {
                if (offer.powerUpObject != null)
                    Destroy(offer.powerUpObject);
            }
        }

        activeOffers.Clear();
    }

    // ---------------------- Weapons ----------------------

    private void GenerateWeaponOffers(Transform player)
    {
        var weapons = new HashSet<Transform>();
        foreach (var knife in player.GetComponentsInChildren<Knife>())
            weapons.Add(knife.transform);
        foreach (var shooter in player.GetComponentsInChildren<SimpleShooter>())
            weapons.Add(shooter.transform);

        foreach (var weapon in weapons)
        {
            var eligibility = weapon.GetComponent<GeneratedUpgradeEligibility>();
            if (eligibility != null && !eligibility.allowGeneratedUpgrades)
                continue;

            int applied = CountAppliedUpgrades<WeaponUpgrades>(weapon);
            int toRoll = Mathf.Min(offersPerWeapon, WeaponUpgrades.MaxUpgrades - applied);
            if (toRoll <= 0) continue;

            var usedTypes = new HashSet<WeaponUpgrades.UpgradeType>();
            for (int i = 0; i < toRoll; i++)
            {
                var go = CreateOfferObject(weapon);
                var upgrade = go.AddComponent<WeaponUpgrades>();
                if (!upgrade.RandomizeAsOffer(usedTypes))
                {
                    Destroy(go);
                    break;
                }
                usedTypes.Add(upgrade.upgradeType);
                RegisterOffer(upgrade.Upgrade, go);
            }
        }
    }

    // ---------------------- Accessories ----------------------

    private void GenerateAccessoryOffers()
    {
        foreach (var accessory in FindObjectsByType<Accessory>())
        {
            // Only root accessories own upgrades (nested ones are merged visuals).
            if (accessory.transform.parent != null &&
                accessory.transform.parent.GetComponentInParent<Accessory>(true) != null)
                continue;

            var eligibility = accessory.GetComponent<GeneratedUpgradeEligibility>();
            if (eligibility != null && !eligibility.allowGeneratedUpgrades)
                continue;

            int applied = CountAppliedUpgrades<AccessoriesUpgrades>(accessory.transform);
            int toRoll = Mathf.Min(offersPerAccessory, AccessoriesUpgrades.MaxUpgrades - applied);
            if (toRoll <= 0) continue;

            var usedTypes = new HashSet<AccessoriesUpgrades.StatUpgradeType>();
            for (int i = 0; i < toRoll; i++)
            {
                var go = CreateOfferObject(accessory.transform);
                var upgrade = go.AddComponent<AccessoriesUpgrades>();
                if (!upgrade.RandomizeAsOffer(usedTypes))
                {
                    Destroy(go);
                    break;
                }
                usedTypes.Add(upgrade.upgradeType);
                RegisterOffer(upgrade.Upgrade, go);
            }
        }
    }

    // ---------------------- Helpers ----------------------

    private static GameObject CreateOfferObject(Transform target)
    {
        // Created inactive so the upgrade's Awake (which applies it) only runs
        // when PowerUpChooser.TryChoosePowerUp activates the object.
        var go = new GameObject("Generated Upgrade");
        go.SetActive(false);
        go.transform.SetParent(target, false);
        return go;
    }

    /// <summary>Counts applied (activated) upgrades under a target; pending inactive offers don't count.</summary>
    private static int CountAppliedUpgrades<T>(Transform target) where T : Component
    {
        int count = 0;
        foreach (var upgrade in target.GetComponentsInChildren<T>(true))
            if (upgrade.gameObject.activeSelf) count++;
        return count;
    }

    private void RegisterOffer(PowerUp offer, GameObject go)
    {
        if (offer == null)
        {
            Destroy(go);
            return;
        }

        activeOffers.Add(offer);
        powerUpChooser.powerUps.Add(offer);
    }
}

/// <summary>
/// Optional per-item override for runtime-generated upgrade drops.
/// Items without this component remain eligible.
/// </summary>
[DisallowMultipleComponent]
public class GeneratedUpgradeEligibility : MonoBehaviour
{
    public bool allowGeneratedUpgrades = true;
}
