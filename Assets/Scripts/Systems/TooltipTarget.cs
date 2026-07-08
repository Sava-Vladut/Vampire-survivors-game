using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public interface ITooltipTextProvider
{
    string GetTooltipText();
}

[DisallowMultipleComponent]
public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string tooltipMessage;

    [Header("Behaviour")]
    [Tooltip("Automatically refresh tooltip text every frame while visible.")]
    [SerializeField] private bool autoUpdate = false; // default = false

    [Header("Append From Other Components")]
    [SerializeField] private bool appendExtraFromHealth = true;
    [SerializeField] private bool appendFromInventoryActivator = true;
    [SerializeField] private bool appendFromTooltipProviders = true;

    [Header("Text Appearance")]
    [Tooltip("Color of the tooltip text.")]
    [SerializeField] private Color textColor = Color.white;

    // Optional: if your TooltipManager wants to follow a specific rect when this is on UI
    [SerializeField] private RectTransform uiAnchorOverride;

    private SimpleHealth health;
    private InventoryButtonActivator invActivator;
    private SimpleInventory playerInventory; // cached reference to player's inventory
    private bool isShowing;

    private void Awake()
    {
        health = GetComponent<SimpleHealth>();
        invActivator = GetComponent<InventoryButtonActivator>();
    }

    // ===== Shared =====
    // Lazily find the player's SimpleInventory located as a child of the Player-tagged GameObject
    private SimpleInventory GetPlayerInventory()
    {
        if (playerInventory != null) return playerInventory;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        // include inactive children when searching
#if UNITY_2023_1_OR_NEWER
        playerInventory = player.GetComponentInChildren<SimpleInventory>(true);
#else
        playerInventory = player.GetComponentInChildren<SimpleInventory>(true);
#endif
        return playerInventory;
    }

    private string BuildTooltipText()
    {
        string full = tooltipMessage;

        // 1) SimpleHealth extras (unchanged from your file)
        if (appendExtraFromHealth && health != null)
        {
            string extra = health.extraTextField; // assumes public on your SimpleHealth

            full += "<sprite name=\"heart_0\"> " + (int)health.currentHealth + "/" + health.maxHealth;

            if (!string.IsNullOrWhiteSpace(extra))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n";
                full += extra;
            }
        }

        // 2) InventoryButtonActivator requirement block
        if (appendFromInventoryActivator && invActivator != null)
        {
            // Use public getters (you'll need to add these to InventoryButtonActivator if not already there)
            string reqName = invActivator.requiredItemName;
            int reqAmt = Mathf.Max(1, invActivator.requiredAmount);

            // Look up how many the player currently has in their inventory
            int haveAmt = 0;
            var inv = GetPlayerInventory();
            if (inv != null && !string.IsNullOrEmpty(reqName))
            {
                haveAmt = inv.GetAmount(reqName);
            }

            string reqBlock = $"<b>Requires</b>: {reqName} x{reqAmt}  (You have: {haveAmt})";

            if (!string.IsNullOrWhiteSpace(reqBlock))
            {
                if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                full += reqBlock;
            }
        }

        if (appendFromTooltipProviders)
        {
            var providers = GetComponents<MonoBehaviour>();
            for (int i = 0; i < providers.Length; i++)
            {
                if (providers[i] == null || providers[i] == this)
                    continue;

                if (providers[i] is ITooltipTextProvider provider)
                {
                    string providerText = provider.GetTooltipText();
                    if (!string.IsNullOrWhiteSpace(providerText))
                    {
                        if (!string.IsNullOrWhiteSpace(full)) full += "\n\n";
                        full += providerText;
                    }
                }
            }
        }


        // 3) Wrap entire text in color
        string colorHex = ColorUtility.ToHtmlStringRGB(textColor);
        full = $"<color=#{colorHex}>{full}</color>";

        return full;
    }

    private void ShowTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr == null) return;

        string full = BuildTooltipText();
        if (string.IsNullOrWhiteSpace(full)) return;

        mgr.ShowTooltip(full, this, uiAnchorOverride != null ? uiAnchorOverride : null);
        isShowing = true;
    }

    private void HideTooltipInternal()
    {
        var mgr = TooltipManager.Instance;
        if (mgr != null) mgr.HideTooltip();
        isShowing = false;
    }

    // ===== World objects (needs Collider or Collider2D) =====
    private void OnMouseEnter() => ShowTooltipInternal();
    private void OnMouseExit() => HideTooltipInternal();

    // ===== uGUI elements =====
    public void OnPointerEnter(PointerEventData eventData) => ShowTooltipInternal();
    public void OnPointerExit(PointerEventData eventData) => HideTooltipInternal();

    // ===== Lifecycle safety =====
    private void OnDisable() => HideTooltipInternal();
    private void OnDestroy() => HideTooltipInternal();

    private void Update()
    {
        if (autoUpdate && isShowing)
        {
            // Rebuild and re-show to update the text while open
            ShowTooltipInternal();
        }
    }
}

public class AppliedUpgradeTooltipProvider : MonoBehaviour, ITooltipTextProvider
{
    [SerializeField] private Transform sourceRoot;
    [SerializeField] private string displayName;
    [SerializeField] private bool includeGeneratedUpgrades = true;

    public void Configure(Transform source, string title)
    {
        sourceRoot = source;
        displayName = title;
    }

    public string GetTooltipText()
    {
        Transform root = sourceRoot != null ? sourceRoot : transform;
        string title = string.IsNullOrWhiteSpace(displayName) ? ResolveDisplayName(root) : displayName;

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{title} Upg</b>");

        int generatedCount = 0;
        if (includeGeneratedUpgrades)
        {
            generatedCount += AppendWeaponUpgrades(sb, root, title);
            generatedCount += AppendAccessoryUpgrades(sb, root, title);

            if (generatedCount == 0)
                sb.AppendLine("No gen upg yet.");
        }

        return sb.ToString().TrimEnd();
    }

    private static int AppendWeaponUpgrades(StringBuilder sb, Transform root, string ownerName)
    {
        if (root == null)
            return 0;

        int count = 0;
        var upgrades = root.GetComponentsInChildren<WeaponUpgrades>(true);
        for (int i = 0; i < upgrades.Length; i++)
        {
            var upgrade = upgrades[i];
            if (!IsApplied(upgrade))
                continue;

            sb.AppendLine("- " + FormatWeaponUpgrade(upgrade, ownerName));
            count++;
        }

        return count;
    }

    private static int AppendAccessoryUpgrades(StringBuilder sb, Transform root, string ownerName)
    {
        if (root == null)
            return 0;

        int count = 0;
        var upgrades = root.GetComponentsInChildren<AccessoriesUpgrades>(true);
        for (int i = 0; i < upgrades.Length; i++)
        {
            var upgrade = upgrades[i];
            if (!IsApplied(upgrade))
                continue;

            sb.AppendLine("- " + FormatAccessoryUpgrade(upgrade, ownerName));
            count++;
        }

        return count;
    }

    private static bool IsApplied(Behaviour upgrade)
    {
        return upgrade != null && upgrade.enabled && upgrade.gameObject.activeInHierarchy;
    }

    private static string FormatWeaponUpgrade(WeaponUpgrades upgrade, string ownerName)
    {
        if (upgrade.Upgrade != null && !string.IsNullOrWhiteSpace(upgrade.Upgrade.powerUpName))
            return FormatPowerUpName(upgrade.Upgrade, ownerName);

        return $"{ShortenUpgradeName(Nicify(upgrade.upgradeType.ToString()), ownerName)} {FormatValue(upgrade.value)}";
    }

    private static string FormatAccessoryUpgrade(AccessoriesUpgrades upgrade, string ownerName)
    {
        if (upgrade.Upgrade != null && !string.IsNullOrWhiteSpace(upgrade.Upgrade.powerUpName))
            return FormatPowerUpName(upgrade.Upgrade, ownerName);

        return $"{ShortenUpgradeName(Nicify(upgrade.upgradeType.ToString()), ownerName)} {FormatValue(upgrade.value)}";
    }

    private static string FormatPowerUpName(PowerUp powerUp, string ownerName)
    {
        string rarityColor = PowerUp.GetRarityColor(powerUp.rarity);
        string name = ShortenUpgradeName(powerUp.powerUpName, ownerName);
        return $"<color={rarityColor}>[{GetRarityShortName(powerUp.rarity)}]</color> {name}";
    }

    private static string GetRarityShortName(PowerUpRarity rarity)
    {
        return rarity switch
        {
            PowerUpRarity.Uncommon => "U",
            PowerUpRarity.Rare => "R",
            PowerUpRarity.Curse => "CUR",
            _ => "C",
        };
    }

    private static string ShortenUpgradeName(string raw, string ownerName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Upg";

        string name = raw.Trim();
        if (!string.IsNullOrWhiteSpace(ownerName))
        {
            string prefix = ownerName.Trim() + " - ";
            if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(prefix.Length).TrimStart();
        }

        name = name.Replace("Tempered Power", "Dmg");
        name = name.Replace("Weapon Mastery", "Dmg");
        name = name.Replace("Keen Edge", "Crit");
        name = name.Replace("Savage Criticals", "Crit Dmg");
        name = name.Replace("Affliction Chance", "Proc");
        name = name.Replace("Potent Affliction", "Proc");
        name = name.Replace("Lingering Curse", "Dur");
        name = name.Replace("Enduring Affliction", "Dur");
        name = name.Replace("Enable Status On Hit", "Status");
        name = name.Replace("Crushing Force", "KB");
        name = name.Replace("Culling Strike", "Cull");
        name = name.Replace("Long Reach", "Reach");
        name = name.Replace("Sweeping Reach", "Reach");
        name = name.Replace("Greater Bloodthirst", "Steal");
        name = name.Replace("Bloodthirst", "Steal");
        name = name.Replace("Wider Impact", "AOE");
        name = name.Replace("Expanding Impact", "AOE");
        name = name.Replace("Greater Aftershock", "Splash");
        name = name.Replace("Aftershock", "Splash");
        name = name.Replace("Echo Edge", "Echo");
        name = name.Replace("Swift Projectiles", "Speed");
        name = name.Replace("Arcane Velocity", "Speed");
        name = name.Replace("Extended Flight", "Life");
        name = name.Replace("Endless Flight", "Life");
        name = name.Replace("Reinforced Shot", "Pierce");
        name = name.Replace("Forked Rounds", "Fork");
        name = name.Replace("Greater Vitality", "HP");
        name = name.Replace("Vitality", "HP");
        name = name.Replace("Trollblood", "Regen");
        name = name.Replace("Iron Skin", "Armor");
        name = name.Replace("Hardened Armor", "Armor");
        name = name.Replace("Nimble Footwork", "Evade");
        name = name.Replace("Elusive Form", "Evade");
        name = name.Replace("Flame Ward", "Fire Res");
        name = name.Replace("Frost Ward", "Cold Res");
        name = name.Replace("Storm Ward", "Ltng Res");
        name = name.Replace("Venom Ward", "Poison Res");
        name = name.Replace("Fleetfoot", "Move");
        name = name.Replace("Bounding Step", "Dash");
        name = name.Replace("Spiked Plate", "Thorns");

        return name;
    }

    private static string FormatValue(float value)
    {
        if (Mathf.Abs(value) < 0.0001f)
            return string.Empty;

        return $"({value:0.##})";
    }

    private static string ResolveDisplayName(Transform root)
    {
        if (root == null)
            return "Item";

        if (root.TryGetComponent(out Accessory accessory) && !string.IsNullOrWhiteSpace(accessory.AccesoryName))
            return accessory.AccesoryName;

        return root.name;
    }

    private static string Nicify(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Upgrade";

        var sb = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(raw[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }

        return sb.ToString();
    }
}
