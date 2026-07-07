using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PowerUpSelectionUI : MonoBehaviour
{
    [Header("UI Setup")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private TMP_Text[] nameTexts;
    [SerializeField] private TMP_Text[] descriptionTexts;
    [SerializeField] private Button[] selectButtons;
    [SerializeField] private Image[] iconImages;

    [Header("Extra Buttons")]
    [SerializeField] private Button[] skipButton;

    [Header("Reroll Button")]
    [SerializeField] private Button rerollButton;
    [Min(0)][SerializeField] private int refreshesPerGame = 1;

    [Header("References")]
    [SerializeField] private PowerUpChooser powerUpChooser;
    [Tooltip("Rolls fresh random upgrade offers each time the selection opens. Auto-found if left empty.")]
    [SerializeField] private RandomUpgradeGenerator upgradeGenerator;
    [SerializeField] private Volume slowMoVolume;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSFX;
    [SerializeField] private AudioClip closeSFX;

    [Header("Defaults")]
    [Tooltip("Default icon to use when a power-up has no icon.")]
    [SerializeField] public Sprite defaultIcon;

    private int[] shownIndices;
    private bool warnedNoDefault;
    private int refreshesRemaining;
    [Header("Behavior")]
    [SerializeField] private bool isFirstSelection = true; // first selection shows weapons only
    [SerializeField] private bool firstSelectionWeaponsOnly = false;

    // Optional: number of choices for the first selection
    [SerializeField] private int firstSelectionCount = 3;

    private void Awake()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        refreshesRemaining = refreshesPerGame;

        if (upgradeGenerator == null && powerUpChooser != null)
            upgradeGenerator = powerUpChooser.GetComponent<RandomUpgradeGenerator>();

        // Existing scenes/prefabs may predate RandomUpgradeGenerator. Keep the
        // selection system functional without requiring every scene to be rewired.
        if (upgradeGenerator == null && powerUpChooser != null)
            upgradeGenerator = powerUpChooser.gameObject.AddComponent<RandomUpgradeGenerator>();

        // Wire up selection buttons safely
        if (selectButtons != null)
        {
            for (int i = 0; i < selectButtons.Length; i++)
            {
                if (selectButtons[i] == null) continue;
                int idx = i; // capture
                selectButtons[i].onClick.RemoveAllListeners();
                selectButtons[i].onClick.AddListener(() => SelectPowerUp(idx));
            }
        }

        if (skipButton != null)
        {
            foreach (var btn in skipButton)
            {
                if (btn == null) continue;
                btn.onClick.AddListener(SkipChoice);
                btn.gameObject.SetActive(false); // hide initially
            }
        }

        // Hook up reroll button
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(RefreshSelection);
            rerollButton.gameObject.SetActive(false); // hidden until selection is shown
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void ShowSelection()
    {
        if (powerUpChooser == null || powerUpChooser.powerUps == null)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No PowerUpChooser assigned!");
            return;
        }

        if (selectButtons == null || selectButtons.Length == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No select buttons assigned.");
            return;
        }

        // Keep lists in sync (chooser owns the logic)
        powerUpChooser.SyncActiveToSelected();

        // Roll fresh random upgrade offers for owned weapons/accessories
        // (also discards offers from a previous open/reroll)
        if (upgradeGenerator != null)
            upgradeGenerator.RefreshOffers();

        if (powerUpChooser.powerUps.Count == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No power-ups available!");
            return;
        }

        // Build eligible candidates strictly by caps/type rules
        // (first selection forces weapons only)
        List<int> candidates = new List<int>();
        for (int i = 0; i < powerUpChooser.powerUps.Count; i++)
        {
            if (!powerUpChooser.CanSelectByIndex(i)) continue;
            if (isFirstSelection && firstSelectionWeaponsOnly && !powerUpChooser.powerUps[i].IsWeapon) continue;
            candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[PowerUpSelectionUI] No eligible power-ups to show (type caps reached).");
            ClosePanel();
            return;
        }

        if (slowMoVolume) slowMoVolume.weight = 1f;
        Time.timeScale = 0f;

        if (selectionPanel != null) selectionPanel.SetActive(true);
        PlaySFX(openSFX);

        int desired = isFirstSelection ? Mathf.Max(1, firstSelectionCount) : 3;
        int slotCount = Mathf.Min(desired, selectButtons.Length, candidates.Count);
        shownIndices = PickRandomUnique(candidates, slotCount);

        // After first presentation, subsequent selections are normal
        if (isFirstSelection)
            isFirstSelection = false;

        if (defaultIcon == null && !warnedNoDefault)
        {
            warnedNoDefault = true;
            Debug.LogWarning("[PowerUpSelectionUI] defaultIcon is not assigned.");
        }

        // Fill visible slots
        for (int i = 0; i < selectButtons.Length; i++)
        {
            bool has = i < shownIndices.Length;

            // Name
            if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                nameTexts[i].text = has ? powerUpChooser.powerUps[shownIndices[i]].powerUpName : string.Empty;

            // Description
            if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
            {
                if (has)
                {
                    var pu = powerUpChooser.powerUps[shownIndices[i]];
                    descriptionTexts[i].text = string.Empty;

                    if (pu.IsUpgrade)
                    {
                        string rarityName = PowerUp.GetRarityDisplayName(pu.rarity).ToUpperInvariant();
                        string rarityColor = PowerUp.GetRarityColor(pu.rarity);
                        descriptionTexts[i].text += $"<b><color={rarityColor}>[{rarityName}]</color></b> ";
                    }

                    if (pu.IsWeapon)
                        descriptionTexts[i].text += "<b>[WEAPON] </b>";

                    if (pu.IsAccessory)
                        descriptionTexts[i].text += "<b>[ACCESSORY] </b>";

                    if (pu.IsUpgrade || pu.IsWeapon || pu.IsAccessory)
                        descriptionTexts[i].text += "\n";

                    descriptionTexts[i].text += pu.powerUpDescription;
                }
                else
                {
                    descriptionTexts[i].text = string.Empty;
                }
            }


            // Icon
            if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
            {
                var img = iconImages[i];
                if (has)
                {
                    var pu = powerUpChooser.powerUps[shownIndices[i]];
                    img.sprite = pu.powerUpIcon != null ? pu.powerUpIcon : defaultIcon;
                    img.enabled = true;
                    img.gameObject.SetActive(true);
                }
                else
                {
                    img.sprite = null;
                    img.enabled = false;
                    img.gameObject.SetActive(false);
                }
            }

            // Button
            if (selectButtons[i] != null)
            {
                selectButtons[i].interactable = has;
                selectButtons[i].gameObject.SetActive(has);
            }
        }

        SetExtraButtonsVisible(true);
    }

    /// <summary>Shows/hides the skip and reroll buttons together.</summary>
    private void SetExtraButtonsVisible(bool visible)
    {
        if (skipButton != null)
        {
            foreach (var btn in skipButton)
            {
                if (btn != null) btn.gameObject.SetActive(visible);
            }
        }

        if (rerollButton != null)
            rerollButton.gameObject.SetActive(visible && refreshesRemaining > 0);
    }

    private void SelectPowerUp(int buttonSlot)
    {
        if (shownIndices == null || buttonSlot < 0 || buttonSlot >= shownIndices.Length) return;

        int powerUpIndex = shownIndices[buttonSlot];
        if (powerUpChooser == null || powerUpChooser.powerUps == null ||
            powerUpIndex < 0 || powerUpIndex >= powerUpChooser.powerUps.Count)
        {
            Debug.LogWarning("[PowerUpSelectionUI] Selected power-up index is no longer valid.");
            ClosePanel();
            return;
        }

        powerUpChooser.TryChoosePowerUp(powerUpIndex);
        ClosePanel();
    }

    private void SkipChoice()
    {
        Debug.Log("[PowerUpSelectionUI] Player skipped the power-up selection.");
        ClosePanel();
    }

    private void RefreshSelection()
    {
        if (refreshesRemaining <= 0) return;
        refreshesRemaining--;
        ShowSelection();
    }

    private void ClosePanel()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        PlaySFX(closeSFX);
        Time.timeScale = 1f;
        if (slowMoVolume) slowMoVolume.weight = 0f;

        // Discard whatever offers were not picked
        if (upgradeGenerator != null)
            upgradeGenerator.ClearOffers();

        shownIndices = null;

        SetExtraButtonsVisible(false);
    }

    /// <summary>
    /// Weighted sampling without replacement: picks up to 'count' unique
    /// power-up indices from 'source', weighted by each power-up's weight.
    /// </summary>
    private int[] PickRandomUnique(List<int> source, int count)
    {
        var result = new List<int>(count);
        var available = new List<int>(source);

        // Cache weights once; keep the two lists index-aligned as we remove picks
        var weights = new List<float>(available.Count);
        float totalWeight = 0f;
        foreach (var idx in available)
        {
            float w = Mathf.Max(0f, powerUpChooser.powerUps[idx].weight);
            weights.Add(w);
            totalWeight += w;
        }

        for (int picks = 0; picks < count && available.Count > 0; picks++)
        {
            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            int chosen = 0;

            for (int i = 0; i < available.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    chosen = i;
                    break;
                }
            }

            result.Add(available[chosen]);
            totalWeight -= weights[chosen];
            available.RemoveAt(chosen);
            weights.RemoveAt(chosen);
        }

        return result.ToArray();
    }
}
