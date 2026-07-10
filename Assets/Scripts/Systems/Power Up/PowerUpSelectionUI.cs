using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Presentation/controller for one power-up selection session. It stores offer
/// references instead of mutable pool indices and delegates rolling/formatting to
/// dedicated services.
/// </summary>
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
    [Tooltip("Builds transient upgrade offers. Auto-created for legacy scenes if missing.")]
    [SerializeField] private RandomUpgradeGenerator upgradeGenerator;
    [SerializeField] private Volume slowMoVolume;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSFX;
    [SerializeField] private AudioClip closeSFX;

    [Header("Defaults")]
    [Tooltip("Default icon to use when a power-up has no icon.")]
    [SerializeField] public Sprite defaultIcon;

    [Header("Generated On-Hit Status")]
    [Tooltip("Starting chance granted when the selector first enables a weapon's on-hit status effect.")]
    [Range(0f, 1f)][SerializeField] private float firstOnHitBaseChance = 0.15f;

    [Header("Behavior")]
    [SerializeField] private bool isFirstSelection = true;
    [SerializeField] private bool firstSelectionWeaponsOnly = false;
    [Min(1)][SerializeField] private int firstSelectionCount = 3;
    [Min(1)][SerializeField] private int choicesPerSelection = 3;

    private readonly List<PowerUp> shownOffers = new();
    private bool warnedNoDefault;
    private int refreshesRemaining;
    private bool sessionOpen;
    private float previousTimeScale = 1f;
    private float previousVolumeWeight;

    public bool IsOpen => sessionOpen;

    private void Awake()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        refreshesRemaining = refreshesPerGame;

        ResolveReferences();
        WireButtons();
        SetExtraButtonsVisible(false);
    }

    private void OnDisable()
    {
        if (sessionOpen)
            ClosePanel(false);
    }

    public void ShowSelection()
    {
        ResolveReferences();
        if (!ValidateConfiguration()) return;

        if (!sessionOpen)
        {
            powerUpChooser.SyncActiveToSelected();
            OpenPanel();
        }

        RefreshOffersAndDisplay();
    }

    private void RefreshOffersAndDisplay()
    {
        bool firstPresentation = isFirstSelection;

        GeneratedUpgradeSettings.Load()?.EnsureAllRanges();
        upgradeGenerator?.RefreshOffers(firstOnHitBaseChance);

        var eligible = new List<PowerUp>();
        IReadOnlyList<PowerUp> available = powerUpChooser.AvailablePowerUps;
        for (int i = 0; i < available.Count; i++)
        {
            PowerUp offer = available[i];
            if (!powerUpChooser.CanSelect(offer)) continue;
            if (firstPresentation && firstSelectionWeaponsOnly && !offer.IsWeapon) continue;
            eligible.Add(offer);
        }

        if (eligible.Count == 0)
        {
            Debug.Log("[PowerUpSelectionUI] No eligible power-ups to show.", this);
            ClosePanel();
            return;
        }

        int desired = firstPresentation ? firstSelectionCount : choicesPerSelection;
        int slots = Mathf.Min(Mathf.Max(1, desired), selectButtons.Length);
        shownOffers.Clear();
        shownOffers.AddRange(PowerUpWeightedSelector.PickContextual(
            eligible,
            slots,
            guaranteeUpgrade: true,
            allowDuplicateFallback: true));

        RenderOffers();
        SetExtraButtonsVisible(true);

        if (firstPresentation)
            isFirstSelection = false;
    }

    private void RenderOffers()
    {
        if (defaultIcon == null && !warnedNoDefault)
        {
            warnedNoDefault = true;
            Debug.LogWarning("[PowerUpSelectionUI] defaultIcon is not assigned.", this);
        }

        for (int i = 0; i < selectButtons.Length; i++)
        {
            bool hasOffer = i < shownOffers.Count;
            PowerUp offer = hasOffer ? shownOffers[i] : null;

            if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                nameTexts[i].text = offer?.powerUpName ?? string.Empty;

            if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
                descriptionTexts[i].text = PowerUpCardFormatter.BuildDescription(offer);

            if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
            {
                Image image = iconImages[i];
                image.sprite = hasOffer ? offer.powerUpIcon ?? defaultIcon : null;
                image.enabled = hasOffer;
                image.gameObject.SetActive(hasOffer);
            }

            Button button = selectButtons[i];
            if (button != null)
            {
                button.interactable = hasOffer;
                button.gameObject.SetActive(hasOffer);
            }
        }
    }

    private void SelectPowerUp(int buttonSlot)
    {
        if (buttonSlot < 0 || buttonSlot >= shownOffers.Count) return;

        PowerUp offer = shownOffers[buttonSlot];
        if (!powerUpChooser.TryChoosePowerUp(offer))
        {
            Debug.LogWarning("[PowerUpSelectionUI] The selected offer could not be applied.", this);
            RefreshOffersAndDisplay();
            return;
        }

        ClosePanel();
    }

    private void SkipChoice()
    {
        Debug.Log("[PowerUpSelectionUI] Player skipped the power-up selection.", this);
        ClosePanel();
    }

    private void RefreshSelection()
    {
        if (!sessionOpen || refreshesRemaining <= 0) return;
        refreshesRemaining--;
        RefreshOffersAndDisplay();
    }

    private void OpenPanel()
    {
        sessionOpen = true;
        previousTimeScale = Time.timeScale;
        previousVolumeWeight = slowMoVolume != null ? slowMoVolume.weight : 0f;

        if (slowMoVolume != null) slowMoVolume.weight = 1f;
        Time.timeScale = 0f;
        if (selectionPanel != null) selectionPanel.SetActive(true);
        PlaySFX(openSFX);
    }

    private void ClosePanel(bool playSound = true)
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (playSound) PlaySFX(closeSFX);

        if (sessionOpen)
        {
            Time.timeScale = previousTimeScale;
            if (slowMoVolume != null) slowMoVolume.weight = previousVolumeWeight;
        }

        upgradeGenerator?.ClearOffers();
        shownOffers.Clear();
        sessionOpen = false;
        SetExtraButtonsVisible(false);
    }

    private void SetExtraButtonsVisible(bool visible)
    {
        if (skipButton != null)
        {
            for (int i = 0; i < skipButton.Length; i++)
                if (skipButton[i] != null)
                    skipButton[i].gameObject.SetActive(visible);
        }

        if (rerollButton != null)
            rerollButton.gameObject.SetActive(visible && refreshesRemaining > 0);
    }

    private void ResolveReferences()
    {
        if (powerUpChooser == null)
            powerUpChooser = GetComponent<PowerUpChooser>();
        if (powerUpChooser == null)
            powerUpChooser = FindAnyObjectByType<PowerUpChooser>();

        if (upgradeGenerator == null && powerUpChooser != null)
            upgradeGenerator = powerUpChooser.GetComponent<RandomUpgradeGenerator>();

        // Compatibility path for existing scenes. New scenes should add this
        // component explicitly so its offer counts are visible in the Inspector.
        if (upgradeGenerator == null && powerUpChooser != null)
            upgradeGenerator = powerUpChooser.gameObject.AddComponent<RandomUpgradeGenerator>();
    }

    private bool ValidateConfiguration()
    {
        if (powerUpChooser == null)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No PowerUpChooser assigned.", this);
            return false;
        }

        if (selectButtons == null || selectButtons.Length == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No select buttons assigned.", this);
            return false;
        }

        return true;
    }

    private void WireButtons()
    {
        if (selectButtons != null)
        {
            for (int i = 0; i < selectButtons.Length; i++)
            {
                if (selectButtons[i] == null) continue;
                int slot = i;
                selectButtons[i].onClick.RemoveAllListeners();
                selectButtons[i].onClick.AddListener(() => SelectPowerUp(slot));
            }
        }

        if (skipButton != null)
        {
            for (int i = 0; i < skipButton.Length; i++)
            {
                if (skipButton[i] == null) continue;
                skipButton[i].onClick.RemoveListener(SkipChoice);
                skipButton[i].onClick.AddListener(SkipChoice);
            }
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(RefreshSelection);
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
