using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AccessoryInventoryPresenter : MonoBehaviour
{
    [SerializeField] private AccessoryInventory inventory;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Transform contentParent;

    private readonly Dictionary<Accessory, GameObject> entries = new();

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponentInParent<AccessoryInventory>();
    }

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.Equipped += Add;
        inventory.Removed += Remove;
        inventory.AccessoryChanged += Refresh;
        for (int i = 0; i < inventory.EquippedAccessories.Count; i++)
            Add(inventory.EquippedAccessories[i]);
    }

    private void OnDisable()
    {
        if (inventory == null) return;
        inventory.Equipped -= Add;
        inventory.Removed -= Remove;
        inventory.AccessoryChanged -= Refresh;
    }

    private void Add(Accessory accessory)
    {
        if (accessory == null || entries.ContainsKey(accessory) || entryPrefab == null || contentParent == null)
            return;

        GameObject entry = Instantiate(entryPrefab, contentParent);
        entries.Add(accessory, entry);

        if (!entry.TryGetComponent(out TooltipTarget _))
            entry.AddComponent<TooltipTarget>();
        AppliedUpgradeTooltipProvider provider = entry.GetComponent<AppliedUpgradeTooltipProvider>();
        if (provider == null) provider = entry.AddComponent<AppliedUpgradeTooltipProvider>();
        provider.Configure(accessory.transform, accessory.DisplayName);
        Refresh(accessory);
    }

    private void Remove(Accessory accessory)
    {
        if (accessory == null || !entries.TryGetValue(accessory, out GameObject entry)) return;
        entries.Remove(accessory);
        if (entry != null) Destroy(entry);
    }

    private void Refresh(Accessory accessory)
    {
        if (accessory == null || !entries.TryGetValue(accessory, out GameObject entry) || entry == null) return;
        TextMeshProUGUI text = entry.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = accessory.BuildDisplayText();
        Transform iconTransform = entry.transform.Find("Icon");
        Image image = iconTransform != null ? iconTransform.GetComponent<Image>() : entry.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = accessory.Icon;
            image.enabled = accessory.Icon != null;
        }
    }
}
