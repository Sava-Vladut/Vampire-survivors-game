using System.Collections.Generic;
using UnityEngine;

//
// Handles a single accessory upgrade entry. Applies a player stat bonus via
// SimpleHealth when its GameObject is activated (via PowerUpChooser.TryChoosePowerUp).
// Offers are rolled at runtime by RandomUpgradeGenerator through RandomizeAsOffer.
//
public class AccessoriesUpgrades : MonoBehaviour
{
    /// <summary>Maximum number of upgrades a single accessory can receive.</summary>
    public const int MaxUpgrades = 20;

    // Serialized by value into scenes/prefabs — do NOT reorder or remove members.
    public enum StatUpgradeType
    {
        None,
        MaxHealthFlat,
        MaxHealthPercent,
        RegenFlat,
        ArmorFlat,
        ArmorPercent,
        EvasionFlat,
        EvasionPercent,
        FireResist,
        ColdResist,
        LightningResist,
        PoisonResist,
        MoveSpeedFlat,
        DashDistanceFlat,
    }

    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Header("Upgrade Settings")]
    public StatUpgradeType upgradeType = StatUpgradeType.None;

    [Tooltip("Flat amount, or a fraction for percent-based types (e.g., 0.25 = 25%).")]
    public float value = 0f;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        ApplyUpgrade();
    }

    // ---------------------- Apply ----------------------

    public void ApplyUpgrade()
    {
        if (upgradeType == StatUpgradeType.None) return;

        if (upgradeType == StatUpgradeType.MoveSpeedFlat ||
            upgradeType == StatUpgradeType.DashDistanceFlat)
        {
            var movement = FindPlayerMovement();
            if (movement == null)
            {
                Debug.LogWarning("[AccessoriesUpgrades] No player Snappy2DController found; movement upgrade not applied.", this);
                return;
            }

            if (upgradeType == StatUpgradeType.MoveSpeedFlat)
                movement.IncreaseMoveSpeed(value);
            else
                movement.IncreaseDashDistance(value);

            return;
        }

        var health = FindPlayerHealth();
        if (health == null)
        {
            Debug.LogWarning("[AccessoriesUpgrades] No player SimpleHealth found; upgrade not applied.", this);
            return;
        }

        switch (upgradeType)
        {
            case StatUpgradeType.MaxHealthFlat:
                health.IncreaseMaxHealth(Mathf.RoundToInt(value));
                break;
            case StatUpgradeType.MaxHealthPercent:
                health.IncreaseMaxHealth(Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * value)));
                break;
            case StatUpgradeType.RegenFlat:
                health.regenRate += value;
                break;
            case StatUpgradeType.ArmorFlat:
                health.GiveArmor(value);
                break;
            case StatUpgradeType.ArmorPercent:
                health.GiveArmor(health.armor * value);
                break;
            case StatUpgradeType.EvasionFlat:
                health.GiveEvasion(value);
                break;
            case StatUpgradeType.EvasionPercent:
                health.GiveEvasion(health.evasion * value);
                break;
            case StatUpgradeType.FireResist:
                health.AddFireResist(value);
                break;
            case StatUpgradeType.ColdResist:
                health.AddColdResist(value);
                break;
            case StatUpgradeType.LightningResist:
                health.AddLightningResist(value);
                break;
            case StatUpgradeType.PoisonResist:
                health.AddPoisonResist(value);
                break;
        }
    }

    // ---------------------- Random generation ----------------------

    /// <summary>
    /// Configures this component as a freshly rolled upgrade offer: picks a random
    /// stat type (minus excludeTypes and percent types without a base stat), rolls
    /// a value, and creates a new PowerUp entry whose powerUpObject is this
    /// GameObject, so selecting it in the UI activates this object and applies it.
    /// Returns false when no stat type is available.
    /// </summary>
    public bool RandomizeAsOffer(ICollection<StatUpgradeType> excludeTypes = null)
    {
        var health = FindPlayerHealth();
        bool isBoots = IsBootsOwner();

        var allowed = new List<StatUpgradeType>();
        foreach (StatUpgradeType t in System.Enum.GetValues(typeof(StatUpgradeType)))
        {
            if (t == StatUpgradeType.None) continue;
            if (excludeTypes != null && excludeTypes.Contains(t)) continue;
            if ((t == StatUpgradeType.MoveSpeedFlat ||
                 t == StatUpgradeType.DashDistanceFlat) && !isBoots) continue;
            // Percent types are useless without a base stat to scale
            if (t == StatUpgradeType.ArmorPercent && (health == null || health.armor <= 0f)) continue;
            if (t == StatUpgradeType.EvasionPercent && (health == null || health.evasion <= 0f)) continue;
            allowed.Add(t);
        }
        if (allowed.Count == 0) return false;

        upgradeType = allowed[Random.Range(0, allowed.Count)];
        value = GetRandomValueForType(upgradeType);

        Upgrade = new PowerUp
        {
            powerUpObject = gameObject,
            IsAccessory = true,
            IsUpgrade = true
        };
        TryAssignIconFromParent();
        SetUpgradeInfo();
        return true;
    }

    private static float GetRandomValueForType(StatUpgradeType t)
    {
        switch (t)
        {
            case StatUpgradeType.MaxHealthFlat: return Mathf.Round(Random.Range(15f, 60f));
            case StatUpgradeType.MaxHealthPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.RegenFlat: return Random.Range(0.10f, 1.50f);
            case StatUpgradeType.ArmorFlat: return Mathf.Round(Random.Range(1f, 6f));
            case StatUpgradeType.ArmorPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.EvasionFlat: return Mathf.Round(Random.Range(2f, 12f));
            case StatUpgradeType.EvasionPercent: return Random.Range(0.05f, 0.25f);
            case StatUpgradeType.FireResist:
            case StatUpgradeType.ColdResist:
            case StatUpgradeType.LightningResist:
            case StatUpgradeType.PoisonResist: return Random.Range(0.05f, 0.20f);
            case StatUpgradeType.MoveSpeedFlat: return Random.Range(0.15f, 0.75f);
            case StatUpgradeType.DashDistanceFlat: return Random.Range(0.25f, 1.50f);
        }
        return 0f;
    }

    // ---------------------- Display text ----------------------

    private const string NumColor = "#8888FF";
    private static string C(string s) => $"<color={NumColor}>{s}</color>";

    private void SetUpgradeInfo()
    {
        if (Upgrade == null) return;

        string flat = C(Mathf.RoundToInt(value).ToString());
        string pct = C((value * 100f).ToString("F0")) + "%";
        string perSec = C(value.ToString("F2")) + "/s";
        string decimalValue = C(value.ToString("F2"));

        (string title, string desc) = upgradeType switch
        {
            StatUpgradeType.MaxHealthFlat => ($"Vitality +{flat}", $"Gain {flat} maximum health and become harder to slay."),
            StatUpgradeType.MaxHealthPercent => ($"Greater Vitality +{pct}", $"Increase your current maximum health by {pct}."),
            StatUpgradeType.RegenFlat => ($"Trollblood +{perSec}", $"Restore an additional {perSec} health every second."),
            StatUpgradeType.ArmorFlat => ($"Iron Skin +{flat}", $"Gain {flat} armor to blunt incoming attacks."),
            StatUpgradeType.ArmorPercent => ($"Hardened Armor +{pct}", $"Increase your current armor by {pct}."),
            StatUpgradeType.EvasionFlat => ($"Nimble Footwork +{flat}", $"Gain {flat} evasion, improving your chance to avoid attacks."),
            StatUpgradeType.EvasionPercent => ($"Elusive Form +{pct}", $"Increase your current evasion by {pct}."),
            StatUpgradeType.FireResist => ($"Flame Ward +{pct}", $"Take {pct} less fire damage."),
            StatUpgradeType.ColdResist => ($"Frost Ward +{pct}", $"Take {pct} less cold damage."),
            StatUpgradeType.LightningResist => ($"Storm Ward +{pct}", $"Take {pct} less lightning damage."),
            StatUpgradeType.PoisonResist => ($"Venom Ward +{pct}", $"Take {pct} less poison damage."),
            StatUpgradeType.MoveSpeedFlat => ($"Fleetfoot +{decimalValue}", $"Move {decimalValue} faster at all times."),
            StatUpgradeType.DashDistanceFlat => ($"Bounding Step +{decimalValue}", $"Dash and blink {decimalValue} farther."),
            _ => ("No Upgrade", "This upgrade slot is empty."),
        };

        // Prefix with the owning accessory's name
        var owner = GetComponentInParent<Accessory>(true);
        string ownerName = owner != null && !string.IsNullOrWhiteSpace(owner.AccesoryName)
            ? owner.AccesoryName
            : (transform.parent != null ? transform.parent.name : null);

        Upgrade.powerUpName = string.IsNullOrEmpty(ownerName) ? title : $"{ownerName} - {title}";
        Upgrade.powerUpDescription = desc;
    }

    private void TryAssignIconFromParent()
    {
        if (Upgrade == null) return;

        var owner = GetComponentInParent<Accessory>(true);
        if (owner != null && owner.icon != null)
            Upgrade.powerUpIcon = owner.icon;
    }

    // ---------------------- Helpers ----------------------

    private SimpleHealth FindPlayerHealth()
    {
        var inParent = GetComponentInParent<SimpleHealth>(true);
        if (inParent != null) return inParent;

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponentInChildren<SimpleHealth>() : null;
    }

    private Snappy2DController FindPlayerMovement()
    {
        var inParent = GetComponentInParent<Snappy2DController>(true);
        if (inParent != null) return inParent;

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponentInChildren<Snappy2DController>() : null;
    }

    private bool IsBootsOwner()
    {
        var owner = GetComponentInParent<Accessory>(true);
        if (owner == null) return false;

        return string.Equals(
            owner.AccesoryName?.Trim(),
            "Boots",
            System.StringComparison.OrdinalIgnoreCase);
    }
}
