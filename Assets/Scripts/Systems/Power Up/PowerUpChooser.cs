using NaughtyAttributes;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Curse
}

[System.Serializable]
public class PowerUp
{
    public string powerUpName;
    [TextArea] public string powerUpDescription;

    [Header("Activation")]
    [Tooltip("Prefab or in-scene GameObject to spawn/enable when selected.")]
    public GameObject powerUpObject;

    [Header("Type")]
    [Tooltip("Treat this power-up as an Accessory (counts toward accessory cap).")]
    public bool IsAccessory;
    [Tooltip("Treat this power-up as a Weapon (counts toward weapon cap).")]
    public bool IsWeapon;
    [Tooltip("This entry upgrades an owned item and does not consume a weapon/accessory slot.")]
    public bool IsUpgrade;

    [Header("Visuals")]
    [Tooltip("Icon representing this power-up. If null, UI will use its default icon.")]
    [ShowAssetPreview] public Sprite powerUpIcon;

    [Header("Rarity")]
    [Tooltip("Multiplier tier for generated upgrade values.")]
    public PowerUpRarity rarity = PowerUpRarity.Common;

    [Header("Spawn Weight")]
    [Tooltip("Relative chance for this power-up to appear in selection. Higher = more common.")]
    [Min(0f)] public float weight = 1f;

    public float RarityMultiplier => GetRarityMultiplier(rarity);

    public static PowerUpRarity RollRandomRarity()
    {
        return GeneratedUpgradeSettings.RollPowerUpRarity();
    }

    public static float GetRarityMultiplier(PowerUpRarity rarity)
    {
        return GeneratedUpgradeSettings.GetPowerUpRarityMultiplier(rarity);
    }

    public static string GetRarityDisplayName(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => "Uncommon",
            PowerUpRarity.Rare => "Rare",
            PowerUpRarity.Curse => "Curse",
            _ => "Common",
        };
    }

    public static string GetRarityColor(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => "#33CC66",
            PowerUpRarity.Rare => "#4D8DFF",
            PowerUpRarity.Curse => "#B84DFF",
            _ => "#D9D9D9",
        };
    }
}

public class PowerUpChooser : MonoBehaviour
{
    [Header("Available & Selected")]
    public List<PowerUp> powerUps = new();
    public List<PowerUp> selectedPowerUps = new();

    [Header("Limits")]
    [Min(0)] public int maxAccessories = 1;
    [Min(0)] public int maxWeapons = 1;

    [Header("Stats UI")]
    [Tooltip("Optional: TextMeshProUGUI that will show 'Accessories: cur/max' and 'Weapons: cur/max'.")]
    [SerializeField] private TextMeshProUGUI statsSummaryText;

    [Header("Curse")]
    [Tooltip("Optional Twitch listener to punish cursed upgrade picks. Auto-found if left empty.")]
    [SerializeField] private TwitchListener twitchListener;

    // Track the actual active instance for each PowerUp (either in-scene object or instantiated prefab)
    private readonly Dictionary<PowerUp, GameObject> spawnedInstances = new();

    public int CurrentAccessories => CountSelected(p => p.IsAccessory && !p.IsUpgrade);
    public int CurrentWeapons => CountSelected(p => p.IsWeapon && !p.IsUpgrade);
    public int MaxAccessories => maxAccessories;
    public int MaxWeapons => maxWeapons;

    public int RemainingAccessorySlots => Mathf.Max(0, maxAccessories - CurrentAccessories);
    public int RemainingWeaponSlots => Mathf.Max(0, maxWeapons - CurrentWeapons);

    private void OnEnable()
    {
        SyncActiveToSelected();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshStatsText();
    }
#endif

    public bool CanSelect(PowerUp pu)
    {
        if (pu == null) return false;
        if (pu.IsUpgrade) return true;
        if (pu.IsAccessory && RemainingAccessorySlots <= 0) return false;
        if (pu.IsWeapon && RemainingWeaponSlots <= 0) return false;
        return true;
    }

    public bool CanSelectByIndex(int index) =>
        index >= 0 && index < powerUps.Count && CanSelect(powerUps[index]);

    /// <summary>
    /// Choose the power-up at index. Spawns/enables its object,
    /// moves it to selected list, and removes it from available list.
    /// </summary>
    public bool TryChoosePowerUp(int index)
    {
        if (!CanSelectByIndex(index)) return false;

        var selected = powerUps[index];

        // Spawn or enable the associated object (if any)
        if (selected.powerUpObject != null)
        {
            GameObject instance;
            if (!selected.powerUpObject.scene.IsValid())
            {
                // Prefab: instantiate and track instance
                instance = Instantiate(selected.powerUpObject);
            }
            else
            {
                // In-scene object: enable and track that object
                instance = selected.powerUpObject;
                if (!instance.activeSelf) instance.SetActive(true);
            }

            spawnedInstances[selected] = instance;
        }

        selectedPowerUps.Add(selected);
        powerUps.RemoveAt(index);

        ApplyCursePenalty(selected);
        RefreshStatsText();
        return true;
    }

    private void ApplyCursePenalty(PowerUp selected)
    {
        if (selected == null || selected.rarity != PowerUpRarity.Curse)
            return;

        TwitchListener listener = twitchListener;
        if (listener == null)
            listener = Object.FindAnyObjectByType<TwitchListener>();

        listener?.ApplyCursePowerUpPenalty();
    }

    /// <summary>
    /// Move any already-active, in-scene objects from powerUps to selectedPowerUps,
    /// and track their instance in spawnedInstances.
    /// </summary>
    public void SyncActiveToSelected()
    {
        if (powerUps == null) return;

        for (int i = powerUps.Count - 1; i >= 0; i--)
        {
            var pu = powerUps[i];
            if (pu == null || pu.powerUpObject == null) continue;

            if (pu.powerUpObject.scene.IsValid() && pu.powerUpObject.activeInHierarchy)
            {
                if (!selectedPowerUps.Contains(pu))
                    selectedPowerUps.Add(pu);

                // Track active in-scene instance if not tracked yet
                if (!spawnedInstances.ContainsKey(pu))
                    spawnedInstances[pu] = pu.powerUpObject;

                powerUps.RemoveAt(i);
            }
        }

        RefreshStatsText();
    }

    /// <summary>
    /// Drop (remove) a weapon from selectedPowerUps.
    /// Disables its active instance (SetActive(false)).
    /// Optionally returns it to the available pool.
    /// </summary>
    public bool TryDropWeapon(PowerUp pu, bool addBackToAvailable = true)
    {
        if (pu == null || !pu.IsWeapon) return false;

        if (!selectedPowerUps.Remove(pu))
            return false;

        // Disable spawned/in-scene instance if we have it
        if (spawnedInstances.TryGetValue(pu, out var inst) && inst != null)
        {
            inst.SetActive(false);
            spawnedInstances.Remove(pu);
        }
        else
        {
            // Fallback: if they never got tracked, try the configured object if it's in-scene
            if (pu.powerUpObject != null && pu.powerUpObject.scene.IsValid())
                pu.powerUpObject.SetActive(false);
        }

        if (addBackToAvailable)
            powerUps.Add(pu);

        RefreshStatsText();
        return true;
    }

    /// <summary>
    /// Drop weapon by index in selectedPowerUps (index must point to a weapon).
    /// </summary>
    public bool TryDropWeaponBySelectedIndex(int selectedIndex, bool addBackToAvailable = true)
    {
        if (selectedIndex < 0 || selectedIndex >= selectedPowerUps.Count) return false;
        var pu = selectedPowerUps[selectedIndex];
        return TryDropWeapon(pu, addBackToAvailable);
    }

    /// <summary>
    /// Picks a random weapon from selectedPowerUps and drops it.
    /// Returns true if something was dropped.
    /// </summary>
    public bool DropRandomWeapon(bool addBackToAvailable = true)
    {
        // Build a temp list of weapons only
        var weapons = ListPool<PowerUp>.Get();
        try
        {
            for (int i = 0; i < selectedPowerUps.Count; i++)
            {
                var pu = selectedPowerUps[i];
                if (pu != null && pu.IsWeapon) weapons.Add(pu);
            }

            if (weapons.Count == 0)
            {
                Debug.LogWarning("[PowerUpChooser] No weapons to drop.");
                return false;
            }

            int pick = Random.Range(0, weapons.Count);
            return TryDropWeapon(weapons[pick], addBackToAvailable);
        }
        finally
        {
            ListPool<PowerUp>.Release(weapons);
        }
    }

    // Context menu: right-click the component header and run this in the Editor
    [ContextMenu("Drop Random Weapon")]
    private void ContextMenuDropRandomWeapon()
    {
        bool ok = DropRandomWeapon(true);
        Debug.Log(ok
            ? "[PowerUpChooser] Dropped a random weapon."
            : "[PowerUpChooser] Failed to drop a weapon (none available).");
    }

    private int CountSelected(System.Predicate<PowerUp> predicate)
    {
        int c = 0;
        for (int i = 0; i < selectedPowerUps.Count; i++)
            if (predicate(selectedPowerUps[i])) c++;
        return c;
    }

    /// <summary>
    /// Updates the bound TextMeshProUGUI with current/max counts.
    /// </summary>
    public void RefreshStatsText()
    {
        if (statsSummaryText == null) return;

        statsSummaryText.text =
            $"Accessories: {CurrentAccessories}/{MaxAccessories}\n" +
            $"Weapons: {CurrentWeapons}/{MaxWeapons}";
    }

    /// <summary>
    /// Adjusts the maximum number of weapons by a delta (can be negative).
    /// Clamps the result to be non-negative and refreshes the UI.
    /// </summary>
    public void AddMaxWeapons(int delta)
    {
        maxWeapons = Mathf.Max(0, maxWeapons + delta);
        RefreshStatsText();
    }

    /// <summary>
    /// Adjusts the maximum number of accessories by a delta (can be negative).
    /// Clamps the result to be non-negative and refreshes the UI.
    /// </summary>
    public void AddMaxAccessories(int delta)
    {
        maxAccessories = Mathf.Max(0, maxAccessories + delta);
        RefreshStatsText();
    }
}

/// <summary>
/// Lightweight list pool to avoid allocs in DropRandomWeapon (optional).
/// Remove if you already use another pooling utility.
/// </summary>
static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>();
    public static void Release(List<T> list)
    {
        list.Clear();
        pool.Push(list);
    }
}
