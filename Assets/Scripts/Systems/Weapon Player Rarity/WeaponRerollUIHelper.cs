using System.Collections.Generic;
using System.Linq;
using TMPro; // optional, only if you show labels with TMP
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class WeaponRerollUIHelper : MonoBehaviour
{
    [Header("Selection UI (Required)")]
    public Button prevButton;
    public Button nextButton;

    [Header("Action Buttons (Optional, in order)")]
    [Tooltip("0=Reroll Rarity+Stats, 1=Reroll Stats, 2=Reroll Random Stat, 3=Reroll Into Another, 4=Reroll All Tiers, 5=Upgrade Rarity (keep stats, add unique), 6=Remove Random Upgrade, 7=Add Random Upgrade")]
    public Button[] actionButtons;

    [Header("Labels & Icon (Optional)")]
    public TMP_Text selectedNameLabel;
    public TMP_Text selectedExtraLabel;
    public Image selectedIcon; // will display weaponSprite if available

    // Cached active controllers
    private readonly List<WeaponRarityController> controllers = new List<WeaponRarityController>();
    private int index = -1;

    // CTRL overlay removed; no toggle state needed

    private void Awake()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() =>
            {
                RefreshControllers();
                SelectPrev();
            });
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                RefreshControllers();
                SelectNext();
            });
        }

        WireActions();

        if (controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

        Debug.Log($"[WeaponRerollUIHelper] Cached {controllers.Count} active WeaponRarityController(s).");
        for (int i = 0; i < controllers.Count; i++)
        {
            var c = controllers[i];
            Debug.Log($" - #{i}: {c.name} | Extra: \"{GetExtraText(c)}\"");
        }
    }

    // CTRL functionality removed; no per-frame input checks needed

    [ContextMenu("Refresh Controllers")]
    public void RefreshControllers()
    {
        controllers.Clear();

        var found = FindObjectsByType<WeaponRarityController>(FindObjectsSortMode.None)
            .Where(c => c != null && c.isActiveAndEnabled);

        controllers.AddRange(found);

        if (controllers.Count == 0)
        {
            index = -1;
        }
        else
        {
            if (index < 0 || index >= controllers.Count)
                index = 0; // Auto-select first if none
        }
    }

    public WeaponRarityController CurrentTarget()
    {
        if (index < 0 || index >= controllers.Count) return null;
        return controllers[index];
    }


    private void OnEnable()
    {
        RefreshControllers();

        // Auto-select if nothing selected but we have weapons
        if (index < 0 && controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

    }

    public void SelectPrev()
    {
        if (controllers.Count == 0) return;
        index = (index - 1 + controllers.Count) % controllers.Count;
        UpdateSelectionUI();
    }

    public void SelectNext()
    {
        if (controllers.Count == 0) return;
        index = (index + 1) % controllers.Count;
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        var target = CurrentTarget();

        // --- Labels ---
        if (selectedNameLabel != null)
            selectedNameLabel.text = target ? target.name : "<none>";

        if (selectedExtraLabel != null)
        {
            if (!target)
            {
                selectedExtraLabel.text = "";
            }
            else
            {
                // Always show the weapon's own extra text
                selectedExtraLabel.text = GetExtraText(target);
            }
        }

        // --- Icon ---
        if (selectedIcon != null)
        {
            if (GetWeaponSprite(target) != null)
            {
                selectedIcon.sprite = GetWeaponSprite(target);
            }
        }

        // --- Buttons ---
        bool hasTarget = target != null;


        if (actionButtons != null)
        {
            foreach (var b in actionButtons)
                if (b != null) b.enabled = hasTarget;
        }
    }


    private void WireActions()
    {
        if (actionButtons == null || actionButtons.Length == 0) return;

        UnityAction[] actions =
        {
            () => { RefreshControllers(); CurrentTarget()?.RerollRarityAndStats(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollStats(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollRandomStat(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollRandomStatIntoAnother(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RandomizeRandomTier(true); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.UpgradeRarityKeepStats(); UpdateSelectionUI(); },

            // NEW 6: Remove a random applied upgrade
            () => { RefreshControllers(); CurrentTarget()?.RemoveRandomUpgrade(); UpdateSelectionUI(); },

            // NEW 7: Add a random applicable upgrade
            () => { RefreshControllers(); CurrentTarget()?.AddRandomUpgrade(); UpdateSelectionUI(); },
        };

        for (int i = 0; i < actionButtons.Length; i++)
        {
            var btn = actionButtons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            if (i < actions.Length) btn.onClick.AddListener(actions[i]);
            // No automatic text setting; you control button visuals in the Inspector
        }
    }

    // CTRL overlay helpers removed




    private string GetExtraText(WeaponRarityController controller)
    {
        if (!controller) return "";

        var shooter = controller.GetComponent<SimpleShooter>();
        if (shooter != null)
        {
            shooter.UpdateStatsText();
            return shooter.statsTextInstance != null ? shooter.statsTextInstance.text : "";
        }


        var knife = controller.GetComponent<Knife>();

        if (knife != null)
        {
            knife.UpdateStatsText();
            return knife.statsTextInstance != null ? knife.statsTextInstance.text : "";
        }
        // Accessory support
        var accessory = controller.GetComponent<Accessory>();
        if (accessory != null)
        {
            // Ensure description/UI are current
            accessory.NotifyRootToRefresh();
            return accessory.statsTextInstance != null ? accessory.statsTextInstance.text : "";
        }
        return "";
    }

    private Sprite GetWeaponSprite(WeaponRarityController controller)
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
