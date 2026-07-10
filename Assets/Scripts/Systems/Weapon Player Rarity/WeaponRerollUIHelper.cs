using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    [Tooltip("0=Reroll Rarity+Stats, 1=Reroll Stats, 2=Reroll Random Stat, 3=Reroll Into Another, 4=Reroll All Tiers, 5=Upgrade Rarity (keep stats, add unique), 6=Remove Random Upgrade, 7=Add Random Upgrade, 8=Tier Upgrade, 9=Choose 1 of 3 Modifiers")]
    public Button[] actionButtons;

    [Header("Labels & Icon (Optional)")]
    public TMP_Text selectedNameLabel;
    public TMP_Text selectedExtraLabel;
    public Image selectedIcon; // will display weaponSprite if available

    [Header("Modifier Choice (Optional)")]
    public GameObject modifierChoicePanel;
    public Button[] modifierChoiceButtons;
    public TMP_Text[] modifierChoiceNameLabels;
    public TMP_Text[] modifierChoiceDescriptionLabels;
    public Button modifierChoiceCancelButton;

    // Cached active controllers
    private readonly List<WeaponRarityController> controllers = new List<WeaponRarityController>();
    private int index = -1;
    private bool showRanges;
    private Coroutine changeAnimation;
    private Vector3 extraLabelBaseScale = Vector3.one;
    private readonly List<ModifierOffer> modifierOffers = new List<ModifierOffer>();
    private WeaponRarityController modifierOfferTarget;

    private static readonly Regex RichTextTag = new Regex("<.*?>", RegexOptions.Compiled);

    private void Awake()
    {
        if (modifierChoicePanel != null)
            modifierChoicePanel.SetActive(false);

        if (selectedExtraLabel != null)
            extraLabelBaseScale = selectedExtraLabel.rectTransform.localScale;

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
        WireModifierChoices();

        if (controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

        Debug.Log($"[WeaponRerollUIHelper] Cached {controllers.Count} active WeaponRarityController(s).");
        for (int i = 0; i < controllers.Count; i++)
        {
            var c = controllers[i];
            Debug.Log($" - #{i}: {c.name} | Extra: \"{WeaponRerollTargetDisplay.GetExtraText(c)}\"");
        }
    }

    private void Update()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrlHeld == showRanges) return;

        StopChangeAnimation();
        showRanges = ctrlHeld;
        UpdateSelectionUI();
    }

    [ContextMenu("Refresh Controllers")]
    public void RefreshControllers()
    {
        var previous = CurrentTarget();
        controllers.Clear();

        var found = FindObjectsByType<WeaponRarityController>()
            .Where(c => c != null && c.isActiveAndEnabled);

        controllers.AddRange(found);

        if (controllers.Count == 0)
        {
            index = -1;
        }
        else
        {
            int preserved = previous != null ? controllers.IndexOf(previous) : -1;
            if (preserved >= 0)
                index = preserved;
            else if (index < 0 || index >= controllers.Count)
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

        if (modifierOfferTarget == null || !modifierOfferTarget.isActiveAndEnabled)
        {
            if (modifierOffers.Count > 0)
                CloseModifierChoices(false);
        }
        else
        {
            RenderModifierOffers();
            if (modifierChoicePanel != null)
                modifierChoicePanel.SetActive(true);
        }

        // Auto-select if nothing selected but we have weapons
        if (index < 0 && controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

    }

    private void OnDisable()
    {
        StopChangeAnimation();
    }

    public void SelectPrev()
    {
        if (controllers.Count == 0) return;
        StopChangeAnimation();
        index = (index - 1 + controllers.Count) % controllers.Count;
        UpdateSelectionUI();
    }

    public void SelectNext()
    {
        if (controllers.Count == 0) return;
        StopChangeAnimation();
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
                string normalStats = WeaponRerollTargetDisplay.GetExtraText(target);
                selectedExtraLabel.text = showRanges
                    ? target.GetStatsTextWithRanges(normalStats)
                    : normalStats;
            }
        }

        // --- Icon ---
        if (selectedIcon != null)
        {
            Sprite sprite = WeaponRerollTargetDisplay.GetSprite(target);
            if (sprite != null)
            {
                selectedIcon.sprite = sprite;
                selectedIcon.enabled = true;
            }
            else
            {
                selectedIcon.sprite = null;
                selectedIcon.enabled = false;
            }
        }

        // --- Buttons ---
        bool hasTarget = target != null;
        bool choosingModifier = modifierOfferTarget != null;

        if (prevButton != null)
            prevButton.interactable = hasTarget && !choosingModifier;
        if (nextButton != null)
            nextButton.interactable = hasTarget && !choosingModifier;


        if (actionButtons != null)
        {
            foreach (var b in actionButtons)
                if (b != null) b.interactable = hasTarget && !choosingModifier;

            if (actionButtons.Length > 9 && actionButtons[9] != null)
                actionButtons[9].interactable = hasTarget && !choosingModifier && target.HasAvailableModifierOffer;
        }
    }


    private void WireActions()
    {
        if (actionButtons == null || actionButtons.Length == 0) return;

        UnityAction[] actions = BuildActionMap();

        for (int i = 0; i < actionButtons.Length; i++)
        {
            var btn = actionButtons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            if (i < actions.Length) btn.onClick.AddListener(actions[i]);
            // No automatic text setting; you control button visuals in the Inspector
        }
    }

    private UnityAction[] BuildActionMap()
    {
        return new UnityAction[]
        {
            () => RunAnimatedAction(target => target.RerollRarityAndStats()),
            () => RunAnimatedAction(target => target.RerollStats()),
            () => RunAnimatedAction(target => target.RerollRandomStat()),
            () => RunAnimatedAction(target => target.RerollRandomStatIntoAnother()),
            () => RunAnimatedAction(target => target.RandomizeRandomTier(true)),
            () => RunAnimatedAction(target => target.UpgradeRarityKeepStats()),
            () => RunAnimatedAction(target => target.RemoveRandomUpgrade()),
            () => RunAnimatedAction(target => target.AddRandomUpgrade()),
            () => RunAnimatedAction(target => target.UpgradeAppliedModifierTiers()),
            OpenModifierChoices,
        };
    }

    private void WireModifierChoices()
    {
        if (modifierChoiceButtons != null)
        {
            for (int i = 0; i < modifierChoiceButtons.Length; i++)
            {
                Button button = modifierChoiceButtons[i];
                if (button == null) continue;

                int slot = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectModifierOffer(slot));
            }
        }

        if (modifierChoiceCancelButton != null)
        {
            modifierChoiceCancelButton.onClick.RemoveAllListeners();
            modifierChoiceCancelButton.onClick.AddListener(() => CloseModifierChoices(true));
        }
    }

    private void OpenModifierChoices()
    {
        RefreshControllers();
        WeaponRarityController target = CurrentTarget();
        if (target == null) return;

        if (modifierChoicePanel == null || modifierChoiceButtons == null || modifierChoiceButtons.Length == 0)
        {
            Debug.LogWarning("[WeaponRerollUIHelper] Modifier choice UI is not configured.", this);
            return;
        }

        IReadOnlyList<ModifierOffer> offers = target.CreateModifierOffers(3);
        if (offers.Count == 0)
        {
            UpdateSelectionUI();
            return;
        }

        StopChangeAnimation();
        modifierOffers.Clear();
        for (int i = 0; i < offers.Count; i++)
            modifierOffers.Add(offers[i]);

        modifierOfferTarget = target;
        RenderModifierOffers();
        modifierChoicePanel.SetActive(true);
        UpdateSelectionUI();
    }

    private void RenderModifierOffers()
    {
        if (modifierChoiceButtons == null) return;

        for (int i = 0; i < modifierChoiceButtons.Length; i++)
        {
            bool hasOffer = i < modifierOffers.Count;
            ModifierOffer offer = hasOffer ? modifierOffers[i] : null;
            Button button = modifierChoiceButtons[i];

            if (modifierChoiceNameLabels != null && i < modifierChoiceNameLabels.Length && modifierChoiceNameLabels[i] != null)
                modifierChoiceNameLabels[i].text = offer?.DisplayName ?? string.Empty;

            if (modifierChoiceDescriptionLabels != null && i < modifierChoiceDescriptionLabels.Length && modifierChoiceDescriptionLabels[i] != null)
                modifierChoiceDescriptionLabels[i].text = offer?.PreviewText ?? string.Empty;

            if (button != null)
            {
                button.interactable = hasOffer;
                button.gameObject.SetActive(hasOffer);
            }
        }
    }

    private void SelectModifierOffer(int slot)
    {
        if (slot < 0 || slot >= modifierOffers.Count || modifierOfferTarget == null)
        {
            CloseModifierChoices(true);
            return;
        }

        string before = WeaponRerollTargetDisplay.GetExtraText(modifierOfferTarget);
        bool appliedOffer = modifierOfferTarget.TryApplyModifierOffer(modifierOffers[slot]);

        CloseModifierChoices(false);
        RefreshControllers();
        UpdateSelectionUI();

        if (appliedOffer)
            AnimateSelectionChange(before);
    }

    private void CloseModifierChoices(bool updateSelection)
    {
        if (modifierChoicePanel != null)
            modifierChoicePanel.SetActive(false);

        modifierOffers.Clear();
        modifierOfferTarget = null;

        if (updateSelection)
            UpdateSelectionUI();
    }

    private void RunAnimatedAction(Action<WeaponRarityController> action)
    {
        RefreshControllers();
        var target = CurrentTarget();
        if (target == null) return;

        string before = WeaponRerollTargetDisplay.GetExtraText(target);
        action(target);
        UpdateSelectionUI();

        AnimateSelectionChange(before);
    }

    private void AnimateSelectionChange(string before)
    {

        if (selectedExtraLabel == null || showRanges) return;

        string after = selectedExtraLabel.text;
        string highlighted = HighlightChangedLines(before, after);
        if (highlighted == after) return;

        StopChangeAnimation();
        changeAnimation = StartCoroutine(AnimateChangedStats(highlighted, after));
    }

    private IEnumerator AnimateChangedStats(string highlightedText, string finalText)
    {
        RectTransform rect = selectedExtraLabel.rectTransform;
        selectedExtraLabel.text = highlightedText;

        const float pulseDuration = 0.24f;
        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pulseDuration);
            float eased = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float pulse = Mathf.Sin(eased * Mathf.PI);
            rect.localScale = extraLabelBaseScale * Mathf.Lerp(1f, 1.035f, pulse);
            yield return null;
        }

        rect.localScale = extraLabelBaseScale;
        yield return new WaitForSecondsRealtime(0.31f);
        selectedExtraLabel.text = finalText;
        changeAnimation = null;
    }

    private static string HighlightChangedLines(string before, string after)
    {
        string[] beforeLines = before.Replace("\r", "").Split('\n');
        string[] afterLines = after.Replace("\r", "").Split('\n');
        var previousByKey = new Dictionary<string, string>();

        foreach (string line in beforeLines)
        {
            string visible = RichTextTag.Replace(line, "").Trim();
            if (visible.Length == 0) continue;
            previousByKey[LineKey(visible)] = visible;
        }

        for (int i = 0; i < afterLines.Length; i++)
        {
            string visible = RichTextTag.Replace(afterLines[i], "").Trim();
            if (visible.Length == 0) continue;

            bool unchanged = previousByKey.TryGetValue(LineKey(visible), out string previous) &&
                             string.Equals(previous, visible, StringComparison.Ordinal);
            if (!unchanged)
                afterLines[i] = $"<mark=#66FF9955>{afterLines[i]}</mark>";
        }

        return string.Join("\n", afterLines);
    }

    private static string LineKey(string visibleLine)
    {
        int colon = visibleLine.IndexOf(':');
        if (colon >= 0)
            return visibleLine.Substring(0, colon).Trim();

        int tierStart = visibleLine.LastIndexOf('(');
        return tierStart > 0 ? visibleLine.Substring(0, tierStart).Trim() : visibleLine;
    }

    private void StopChangeAnimation()
    {
        if (changeAnimation != null)
        {
            StopCoroutine(changeAnimation);
            changeAnimation = null;
        }

        if (selectedExtraLabel != null)
            selectedExtraLabel.rectTransform.localScale = extraLabelBaseScale;
    }

}
