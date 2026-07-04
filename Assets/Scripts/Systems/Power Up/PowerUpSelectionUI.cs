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
    [SerializeField] private Button rerollButton; // new button

    [Header("References")]
    [SerializeField] private PowerUpChooser powerUpChooser;
    [SerializeField] private Volume slowMoVolume;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSFX;
    [SerializeField] private AudioClip closeSFX;

    [Header("Defaults")]
    [Tooltip("Default icon to use when a power-up has no icon.")]
    [SerializeField] public Sprite defaultIcon;

    private PowerUp[] shownPowerUps;
    private bool warnedNoDefault;
    [Header("Behavior")]
    [SerializeField] private bool isFirstSelection = true; // first selection shows weapons only

    // Optional: number of choices for the first selection
    [SerializeField] private int firstSelectionCount = 3;

    private void Awake()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);

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
            rerollButton.onClick.AddListener(() => ShowSelection()); // simply calls ShowSelection again
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
        if (powerUpChooser == null || powerUpChooser.powerUps == null || powerUpChooser.powerUps.Count == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No power-ups available!");
            return;
        }

        if (selectButtons == null || selectButtons.Length == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No select buttons assigned.");
            return;
        }

        // Keep lists in sync (chooser owns the logic)
        powerUpChooser.SyncActiveToSelected(); // NEW

        // Build eligible candidates strictly by caps/type rules
        List<int> candidates = new List<int>();
        if (isFirstSelection)
        {
            // First selection: force weapons only
            for (int i = 0; i < powerUpChooser.powerUps.Count; i++)
            {
                if (powerUpChooser.CanSelectByIndex(i) && powerUpChooser.powerUps[i].IsWeapon)
                    candidates.Add(i);
            }
        }
        else
        {
            for (int i = 0; i < powerUpChooser.powerUps.Count; i++)
            {
                if (powerUpChooser.CanSelectByIndex(i))
                    candidates.Add(i);
            }
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
        int[] shownIndices = PickRandomUnique(candidates, slotCount);

        // Snapshot the actual PowerUp references now, not just their indices: the
        // chooser's list can be mutated (e.g. by a PowerUpInserter) while this panel
        // is open, which would make stale indices point at the wrong entry.
        shownPowerUps = new PowerUp[shownIndices.Length];
        for (int i = 0; i < shownIndices.Length; i++)
            shownPowerUps[i] = powerUpChooser.powerUps[shownIndices[i]];

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
            bool has = i < shownPowerUps.Length;

            // Name
            if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                nameTexts[i].text = has ? shownPowerUps[i].powerUpName : string.Empty;

            // Description (with bold [weight] prefix)
            if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
            {
                if (has)
                {
                    var pu = shownPowerUps[i];
                    string weightStr = pu.weight.ToString("0.##");
                    descriptionTexts[i].text = $"<b>[<sprite name=\"chest\">{weightStr}] </b>";

                    if (pu.IsWeapon)
                        descriptionTexts[i].text += "<b>[WEAPON] </b>";

                    if (pu.IsAccessory)
                        descriptionTexts[i].text += "<b>[ACCESSORY] </b>";

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
                    var pu = shownPowerUps[i];
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

        // Show skip button
        if (skipButton != null)
        {
            foreach (var btn in skipButton)
            {
                if (btn != null) btn.gameObject.SetActive(true);
            }
        }

        // Show reroll button
        if (rerollButton != null)
            rerollButton.gameObject.SetActive(true);
    }

    private void SelectPowerUp(int buttonSlot)
    {
        if (shownPowerUps == null || buttonSlot < 0 || buttonSlot >= shownPowerUps.Length) return;

        var pu = shownPowerUps[buttonSlot];
        if (powerUpChooser == null || !powerUpChooser.TryChoosePowerUp(pu))
        {
            Debug.LogWarning("[PowerUpSelectionUI] Selected power-up is no longer available.");
        }

        ClosePanel();
    }

    private void SkipChoice()
    {
        Debug.Log("[PowerUpSelectionUI] Player skipped the power-up selection.");
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        PlaySFX(closeSFX);
        Time.timeScale = 1f;
        if (slowMoVolume) slowMoVolume.weight = 0f;

        shownPowerUps = null;

        if (skipButton != null)
        {
            foreach (var btn in skipButton)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }
        }

        if (rerollButton != null)
            rerollButton.gameObject.SetActive(false);
    }

    private int[] PickRandomUnique(List<int> source, int count)
    {
        var result = new List<int>(count);
        var available = new List<int>(source);

        for (int picks = 0; picks < count && available.Count > 0; picks++)
        {
            float totalWeight = 0f;
            foreach (var idx in available)
                totalWeight += Mathf.Max(0f, powerUpChooser.powerUps[idx].weight);

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            int chosenIndex = available[0];

            foreach (var idx in available)
            {
                cumulative += Mathf.Max(0f, powerUpChooser.powerUps[idx].weight);
                if (roll <= cumulative)
                {
                    chosenIndex = idx;
                    break;
                }
            }

            result.Add(chosenIndex);
            available.Remove(chosenIndex);
        }

        return result.ToArray();
    }
}
