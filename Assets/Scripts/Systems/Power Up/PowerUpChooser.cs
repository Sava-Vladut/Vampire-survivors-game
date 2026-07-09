using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns the available pool and the player's selected power-ups. Selection rules,
/// activation, and explicit selection effects are coordinated here; presentation
/// and random offer generation live in separate classes.
/// </summary>
public class PowerUpChooser : MonoBehaviour
{
    [Header("Available & Selected")]
    public List<PowerUp> powerUps = new();
    public List<PowerUp> selectedPowerUps = new();

    [Header("Limits")]
    [Min(0)] public int maxAccessories = 1;
    [Min(0)] public int maxWeapons = 1;

    [Header("Stats UI")]
    [Tooltip("Optional: TextMeshProUGUI that will show current and maximum item counts.")]
    [SerializeField] private TextMeshProUGUI statsSummaryText;

    [Header("Curse")]
    [Tooltip("Optional Twitch listener to punish cursed upgrade picks. Auto-found if left empty.")]
    [SerializeField] private TwitchListener twitchListener;

    [Header("Optional Data Catalog")]
    [Tooltip("Reusable asset-backed offers to add alongside legacy scene entries.")]
    [SerializeField] private PowerUpCatalog initialCatalog;
    [SerializeField] private bool loadCatalogOnAwake = true;

    [Header("Runtime Instances")]
    [Tooltip("Player receiving selection effects. Auto-resolved from the Player tag when empty.")]
    [SerializeField] private Transform playerRoot;
    [Tooltip("Parent for instantiated accessory prefabs. Defaults to the resolved player root.")]
    [SerializeField] private Transform accessoryInstanceParent;

    private readonly Dictionary<PowerUp, GameObject> spawnedInstances = new();

    public event Action<PowerUp, GameObject> PowerUpSelected;
    public event Action<PowerUp> PowerUpDropped;

    public IReadOnlyList<PowerUp> AvailablePowerUps => powerUps;
    public IReadOnlyList<PowerUp> SelectedPowerUps => selectedPowerUps;

    public int CurrentAccessories => CountSelected(p => p.IsAccessory && !p.IsUpgrade);
    public int CurrentWeapons => CountSelected(p => p.IsWeapon && !p.IsUpgrade);
    public int MaxAccessories => maxAccessories;
    public int MaxWeapons => maxWeapons;
    public int RemainingAccessorySlots => Mathf.Max(0, maxAccessories - CurrentAccessories);
    public int RemainingWeaponSlots => Mathf.Max(0, maxWeapons - CurrentWeapons);
    public Transform PlayerRoot => ResolvePlayerRoot();

    private void Awake()
    {
        powerUps ??= new List<PowerUp>();
        selectedPowerUps ??= new List<PowerUp>();

        if (loadCatalogOnAwake && initialCatalog != null)
            AddCatalog(initialCatalog);
    }

    private void OnEnable() => SyncActiveToSelected();

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxAccessories = Mathf.Max(0, maxAccessories);
        maxWeapons = Mathf.Max(0, maxWeapons);
        RefreshStatsText();
    }
#endif

    public bool CanSelect(PowerUp offer)
    {
        if (offer == null) return false;
        if (offer.IsUpgrade) return true;
        if (offer.IsAccessory && RemainingAccessorySlots <= 0) return false;
        if (offer.IsWeapon && RemainingWeaponSlots <= 0) return false;
        return true;
    }

    public bool CanSelectByIndex(int index) =>
        index >= 0 && index < powerUps.Count && CanSelect(powerUps[index]);

    public bool TryChoosePowerUp(int index)
    {
        return index >= 0 && index < powerUps.Count && TryChoosePowerUp(powerUps[index]);
    }

    public bool TryChoosePowerUp(PowerUp offer)
    {
        if (offer == null || !powerUps.Contains(offer) || !CanSelect(offer))
            return false;

        if (!TryActivateOffer(offer, out GameObject instance))
            return false;

        spawnedInstances[offer] = instance;
        selectedPowerUps.Add(offer);
        powerUps.Remove(offer);

        ApplyCursePenalty(offer);
        RefreshStatsText();
        PowerUpSelected?.Invoke(offer, instance);
        return true;
    }

    public bool TryAddAvailable(PowerUp offer, bool allowDuplicateIdentity = false)
    {
        if (offer == null) return false;

        if (!allowDuplicateIdentity)
        {
            if (ContainsIdentity(powerUps, offer) || ContainsIdentity(selectedPowerUps, offer))
                return false;
        }

        powerUps.Add(offer);
        return true;
    }

    public bool RemoveAvailable(PowerUp offer) => offer != null && powerUps.Remove(offer);

    public void AddCatalog(PowerUpCatalog catalog)
    {
        if (catalog == null || catalog.Entries == null) return;

        for (int i = 0; i < catalog.Entries.Length; i++)
        {
            PowerUpDefinition definition = catalog.Entries[i];
            if (definition != null)
                TryAddAvailable(definition.CreateOffer());
        }
    }

    /// <summary>
    /// Moves already-active scene entries into the selected collection. This keeps
    /// old scenes compatible while new content can use prefab-backed definitions.
    /// </summary>
    public void SyncActiveToSelected()
    {
        if (powerUps == null) return;

        for (int i = powerUps.Count - 1; i >= 0; i--)
        {
            PowerUp offer = powerUps[i];
            if (offer?.powerUpObject == null) continue;

            GameObject configuredObject = offer.powerUpObject;
            if (!configuredObject.scene.IsValid() || !configuredObject.activeInHierarchy)
                continue;

            if (!selectedPowerUps.Contains(offer))
                selectedPowerUps.Add(offer);

            spawnedInstances[offer] = configuredObject;
            powerUps.RemoveAt(i);
        }

        RefreshStatsText();
    }

    public bool TryDropWeapon(PowerUp offer, bool addBackToAvailable = true)
    {
        if (offer == null || !offer.IsWeapon || offer.IsUpgrade)
            return false;

        if (!selectedPowerUps.Remove(offer))
            return false;

        DeactivateTrackedInstance(offer);

        if (addBackToAvailable && !powerUps.Contains(offer))
            powerUps.Add(offer);

        RefreshStatsText();
        PowerUpDropped?.Invoke(offer);
        return true;
    }

    /// <summary>Removes any selected entry while keeping instance tracking consistent.</summary>
    public bool TryRemoveSelected(PowerUp offer, bool disableInstance, bool destroyInstance)
    {
        if (offer == null || !selectedPowerUps.Remove(offer))
            return false;

        if (spawnedInstances.TryGetValue(offer, out GameObject instance))
        {
            if (instance != null)
            {
                if (destroyInstance) Destroy(instance);
                else if (disableInstance) instance.SetActive(false);
            }
            spawnedInstances.Remove(offer);
        }
        else if (offer.powerUpObject != null && offer.powerUpObject.scene.IsValid())
        {
            if (destroyInstance) Destroy(offer.powerUpObject);
            else if (disableInstance) offer.powerUpObject.SetActive(false);
        }

        RefreshStatsText();
        PowerUpDropped?.Invoke(offer);
        return true;
    }

    public bool TryDropWeaponBySelectedIndex(int selectedIndex, bool addBackToAvailable = true)
    {
        if (selectedIndex < 0 || selectedIndex >= selectedPowerUps.Count) return false;
        return TryDropWeapon(selectedPowerUps[selectedIndex], addBackToAvailable);
    }

    public bool DropRandomWeapon(bool addBackToAvailable = true)
    {
        var weapons = ListPool<PowerUp>.Get();
        try
        {
            for (int i = 0; i < selectedPowerUps.Count; i++)
            {
                PowerUp offer = selectedPowerUps[i];
                if (offer != null && offer.IsWeapon && !offer.IsUpgrade)
                    weapons.Add(offer);
            }

            if (weapons.Count == 0)
            {
                Debug.LogWarning("[PowerUpChooser] No equipped weapons to drop.", this);
                return false;
            }

            return TryDropWeapon(weapons[UnityEngine.Random.Range(0, weapons.Count)], addBackToAvailable);
        }
        finally
        {
            ListPool<PowerUp>.Release(weapons);
        }
    }

    [ContextMenu("Drop Random Weapon")]
    private void ContextMenuDropRandomWeapon()
    {
        bool success = DropRandomWeapon(true);
        Debug.Log(success
            ? "[PowerUpChooser] Dropped a random weapon."
            : "[PowerUpChooser] Failed to drop a weapon (none available).", this);
    }

    public void RefreshStatsText()
    {
        if (statsSummaryText == null) return;
        statsSummaryText.text =
            $"Accessories: {CurrentAccessories}/{MaxAccessories}\n" +
            $"Weapons: {CurrentWeapons}/{MaxWeapons}";
    }

    public void AddMaxWeapons(int delta)
    {
        maxWeapons = Mathf.Max(0, maxWeapons + delta);
        RefreshStatsText();
    }

    public void AddMaxAccessories(int delta)
    {
        maxAccessories = Mathf.Max(0, maxAccessories + delta);
        RefreshStatsText();
    }

    private bool TryActivateOffer(PowerUp offer, out GameObject instance)
    {
        instance = null;
        GameObject configuredObject = offer.powerUpObject;
        if (configuredObject == null)
            return true;

        bool isSceneObject = configuredObject.scene.IsValid();
        bool wasActive = isSceneObject && configuredObject.activeSelf;

        Transform parent = !isSceneObject && offer.IsAccessory ? ResolveAccessoryInstanceParent() : null;
        instance = isSceneObject ? configuredObject : Instantiate(configuredObject, parent);
        if (instance == null) return false;

        if (!instance.activeSelf)
            instance.SetActive(true);

        var context = new PowerUpSelectionContext(this, offer, instance, ResolvePlayerRoot());
        MonoBehaviour[] behaviours = instance.GetComponents<MonoBehaviour>();

        try
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPowerUpSelectionEffect effect && !effect.TryApply(context))
                {
                    RollBackActivation(instance, isSceneObject, wasActive);
                    instance = null;
                    return false;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, instance);
            RollBackActivation(instance, isSceneObject, wasActive);
            instance = null;
            return false;
        }

        return true;
    }

    private void DeactivateTrackedInstance(PowerUp offer)
    {
        if (spawnedInstances.TryGetValue(offer, out GameObject instance) && instance != null)
        {
            if (offer.powerUpObject != null && offer.powerUpObject.scene.IsValid())
                instance.SetActive(false);
            else
                Destroy(instance);
            spawnedInstances.Remove(offer);
            return;
        }

        if (offer.powerUpObject != null && offer.powerUpObject.scene.IsValid())
            offer.powerUpObject.SetActive(false);
    }

    private static void RollBackActivation(GameObject instance, bool isSceneObject, bool wasActive)
    {
        if (instance == null) return;
        if (isSceneObject)
        {
            if (!wasActive) instance.SetActive(false);
        }
        else
        {
            Destroy(instance);
        }
    }

    private void ApplyCursePenalty(PowerUp selected)
    {
        if (selected == null || selected.rarity != PowerUpRarity.Curse)
            return;

        TwitchListener listener = twitchListener != null
            ? twitchListener
            : FindAnyObjectByType<TwitchListener>();
        listener?.ApplyCursePowerUpPenalty();
    }

    private Transform ResolvePlayerRoot()
    {
        if (playerRoot != null) return playerRoot;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerRoot = player.transform;
        return playerRoot;
    }

    private Transform ResolveAccessoryInstanceParent()
    {
        if (accessoryInstanceParent != null) return accessoryInstanceParent;
        Transform player = ResolvePlayerRoot();
        if (player == null) return null;
        Transform existing = player.Find("Accessory Runtime");
        accessoryInstanceParent = existing != null ? existing : player;
        return accessoryInstanceParent;
    }

    private int CountSelected(Predicate<PowerUp> predicate)
    {
        int count = 0;
        for (int i = 0; i < selectedPowerUps.Count; i++)
        {
            PowerUp offer = selectedPowerUps[i];
            if (offer != null && predicate(offer)) count++;
        }
        return count;
    }

    private static bool ContainsIdentity(List<PowerUp> list, PowerUp candidate)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].HasSameIdentity(candidate))
                return true;
        return false;
    }
}

internal static class ListPool<T>
{
    private static readonly Stack<List<T>> Pool = new();
    public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>();

    public static void Release(List<T> list)
    {
        list.Clear();
        Pool.Push(list);
    }
}
