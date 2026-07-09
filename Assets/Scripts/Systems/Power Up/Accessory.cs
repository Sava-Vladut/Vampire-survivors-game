using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

public interface IAccessoryDescriptionProvider
{
    string GetAccessoryDescriptionLine();
}

/// <summary>
/// Runtime identity and equip coordinator for one accessory. Gameplay mutations are
/// delegated to typed IAccessoryEquipEffect components and are never run from Awake.
/// </summary>
[DisallowMultipleComponent]
public sealed class Accessory : MonoBehaviour, IPowerUpSelectionEffect
{
    [Header("Presentation fallback")]
    [FormerlySerializedAs("AccesoryName")]
    [SerializeField] private string displayName;
    [FormerlySerializedAs("AccesoryDescription")]
    [TextArea, SerializeField] private string baseDescription;
    [FormerlySerializedAs("icon")]
    [SerializeField] private Sprite icon;

    [Header("Generated upgrades")]
    [SerializeField] private AccessoryUpgradeProfile upgradeProfile;

    // Retained only so old prefabs deserialize cleanly before the editor migration
    // copies these shared references to AccessoryInventoryPresenter.
    [FormerlySerializedAs("statsTextPrefab"), HideInInspector, SerializeField]
    private GameObject legacyStatsTextPrefab;
    [FormerlySerializedAs("uiParent"), HideInInspector, SerializeField]
    private Transform legacyUiParent;

    private readonly List<IAccessoryEquipEffect> equipEffects = new();
    private AccessoryInventory inventory;
    private bool equipped;

    public event Action<Accessory> Changed;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string BaseDescription => baseDescription ?? string.Empty;
    public Sprite Icon => icon;
    public AccessoryUpgradeProfile UpgradeProfile => upgradeProfile;
    public int MaxUpgrades => upgradeProfile != null ? upgradeProfile.MaxUpgrades : AccessoriesUpgrades.MaxUpgrades;
    public bool IsEquipped => equipped;

    // Source-compatible accessors for older callers. New code uses the correctly
    // spelled, read-only presentation API above.
    [Obsolete("Use DisplayName")]
    public string AccesoryName { get => displayName; set => displayName = value; }
    [Obsolete("Use BaseDescription/SetDescription")]
    public string AccesoryDescription { get => baseDescription; set => baseDescription = value; }

    public bool TryApply(PowerUpSelectionContext selectionContext)
    {
        if (equipped)
            return true;

        Transform playerRoot = selectionContext.PlayerRoot;
        if (playerRoot == null)
        {
            Debug.LogWarning($"[Accessory] Cannot equip {name}: no player root was resolved.", this);
            return false;
        }

        ApplyOfferPresentation(selectionContext.Offer);

        var context = new AccessoryEquipContext(selectionContext, this, playerRoot);
        inventory = context.Inventory;
        if (inventory == null)
        {
            Debug.LogWarning($"[Accessory] Cannot equip {DisplayName}: player has no AccessoryInventory.", this);
            return false;
        }

        CollectEquipEffects();
        for (int i = 0; i < equipEffects.Count; i++)
        {
            if (!equipEffects[i].TryEquip(context))
            {
                Debug.LogWarning($"[Accessory] Failed to equip {DisplayName}: {equipEffects[i].GetType().Name} rejected the context.", this);
                return false;
            }
        }

        equipped = true;
        inventory.Register(this);
        MarkChanged();
        return true;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Unregister(this);
    }

    private void OnEnable()
    {
        if (equipped && inventory != null)
            inventory.Register(this);
    }

    private void ApplyOfferPresentation(PowerUp offer)
    {
        if (offer == null)
            return;

        if (!string.IsNullOrWhiteSpace(offer.powerUpName))
            displayName = offer.powerUpName.Trim();
        if (!string.IsNullOrWhiteSpace(offer.powerUpDescription))
            baseDescription = offer.powerUpDescription.Trim();
        if (offer.powerUpIcon != null)
            icon = offer.powerUpIcon;
    }

    private void CollectEquipEffects()
    {
        equipEffects.Clear();
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] is IAccessoryEquipEffect effect)
                equipEffects.Add(effect);

        equipEffects.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public int CountAppliedUpgrades()
    {
        int count = 0;
        AccessoriesUpgrades[] upgrades = GetComponentsInChildren<AccessoriesUpgrades>(true);
        for (int i = 0; i < upgrades.Length; i++)
            if (upgrades[i] != null && upgrades[i].HasApplied)
                count++;
        return count;
    }

    public string BuildDisplayText()
    {
        var sb = new StringBuilder(256);
        sb.AppendLine($"<b>{DisplayName}</b>");
        sb.AppendLine($"Upgrades: <color=#8888FF>{CountAppliedUpgrades()}</color>/<color=#8888FF>{MaxUpgrades}</color>");

        string description = BuildCombinedDescription();
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine(description);
        return sb.ToString();
    }

    public string BuildCombinedDescription()
    {
        var sb = new StringBuilder(256);

        MonoBehaviour[] providers = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < providers.Length; i++)
        {
            MonoBehaviour behaviour = providers[i];
            if (behaviour == null || !behaviour.enabled || !behaviour.gameObject.activeInHierarchy)
                continue;
            if (behaviour is not IAccessoryDescriptionProvider provider)
                continue;

            string line = provider.GetAccessoryDescriptionLine();
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line.Trim());
        }

        // The catalog description belongs on the selection card, not the compact
        // equipped-accessory panel. Keep only the rarity block that the explicit
        // rarity effect appends to this field at runtime.
        if (!string.IsNullOrWhiteSpace(baseDescription))
        {
            string withoutRarity = RarityTextFormatter.RemoveLastRaritySection(baseDescription);
            string rarityBlock = baseDescription.Substring(withoutRarity.Length).Trim();
            if (!string.IsNullOrWhiteSpace(rarityBlock))
                sb.AppendLine(rarityBlock);
        }

        return AccessoryDescriptionFormatter.CombineStatLinesKeepingRarityBlock(sb.ToString().TrimEnd());
    }

    public void MarkChanged()
    {
        Changed?.Invoke(this);
        inventory?.NotifyChanged(this);
    }

    // Compatibility entry point for older systems while they migrate to Changed.
    public void NotifyRootToRefresh() => GetRootAccessory().MarkChanged();

    public void SetDescription(string description)
    {
        baseDescription = description;
        MarkChanged();
    }

    private Accessory GetRootAccessory()
    {
        Accessory root = this;
        Transform current = transform.parent;
        while (current != null)
        {
            if (current.TryGetComponent(out Accessory candidate))
                root = candidate;
            current = current.parent;
        }
        return root;
    }

    public GameObject LegacyStatsTextPrefab => legacyStatsTextPrefab;
    public Transform LegacyUiParent => legacyUiParent;
}

public static class AccessoryDescriptionFormatter
{
    private static readonly Regex StatLineRegex = new(
        @"^\s*([+\-]?\d+(?:\.\d+)?)\s*(%?)\s+([A-Za-z][A-Za-z\s/_\-\.]*)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string CombineStatLinesKeepingRarityBlock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string withoutRarity = RarityTextFormatter.RemoveLastRaritySection(raw);
        string combined = CombineStatLines(withoutRarity.TrimEnd());
        string rarityBlock = raw.Substring(withoutRarity.Length).Trim();
        if (string.IsNullOrWhiteSpace(rarityBlock)) return combined;
        return string.IsNullOrWhiteSpace(combined) ? rarityBlock : combined.TrimEnd() + "\n" + rarityBlock;
    }

    public static string CombineStatLines(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var order = new List<string>();
        var values = new Dictionary<string, float>();
        var percent = new Dictionary<string, bool>();
        var unparsed = new List<string>();

        foreach (string line in raw.Split('\n'))
        foreach (string part in line.Split(','))
        {
            string piece = part.Trim();
            if (piece.Length == 0) continue;
            if (piece is "===AccessoryBonuses===" or "===/AccessoryBonuses===")
                continue;

            Match match = StatLineRegex.Match(piece);
            if (!match.Success || !float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                unparsed.Add(piece);
                continue;
            }

            bool isPercent = match.Groups[2].Value == "%";
            string normalized = NormalizeStatName(match.Groups[3].Value);
            string key = (isPercent ? "%" : string.Empty) + normalized;
            if (!values.ContainsKey(key))
            {
                order.Add(key);
                values[key] = 0f;
                percent[key] = isPercent;
            }
            values[key] += value;
        }

        var output = new List<string>();
        for (int i = 0; i < order.Count; i++)
        {
            string key = order[i];
            float value = values[key];
            if (Mathf.Approximately(value, 0f)) continue;
            bool isPercent = percent[key];
            string name = isPercent && key.StartsWith("%") ? key.Substring(1) : key;
            output.Add($"{(value > 0f ? "+" : string.Empty)}{FormatNumber(value)}{(isPercent ? "%" : string.Empty)} {ToTitleCase(name)}");
        }
        output.AddRange(unparsed);
        return string.Join("\n", output);
    }

    private static string NormalizeStatName(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");

    private static string ToTitleCase(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);

    private static string FormatNumber(float value) =>
        Mathf.Abs(value - Mathf.Round(value)) < 0.0001f
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
