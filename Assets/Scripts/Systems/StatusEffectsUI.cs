using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusEffectsUI : MonoBehaviour
{
    [Serializable]
    public class IconMapping
    {
        public StatusEffectSystem.StatusType type;
        public Sprite sprite;
    }

    private class ActiveIcon
    {
        public Image image;
        public TMP_Text stackText;
        public float startDuration;
    }

    [Header("References")]
    [SerializeField] private StatusEffectSystem target;     // If null, auto-find on this GameObject
    [SerializeField] private Transform iconsParent;         // Parent under a Canvas/Panel
    [SerializeField] private Image iconPrefab;              // Prefab with Image (Type=Filled)

    [Header("Sprites for statuses to show")]
    [SerializeField] private List<IconMapping> icons = new();

    private readonly Dictionary<StatusEffectSystem.StatusType, ActiveIcon> activeIcons = new();
    private Dictionary<StatusEffectSystem.StatusType, Sprite> spriteByType;

    private void Awake()
    {
        if (!target) target = GetComponent<StatusEffectSystem>();
        if (!target)
        {
            Debug.LogWarning("[StatusEffectsUI] No StatusEffectSystem target found.");
            enabled = false;
            return;
        }

        spriteByType = new Dictionary<StatusEffectSystem.StatusType, Sprite>(icons.Count);
        foreach (var m in icons)
        {
            if (!spriteByType.ContainsKey(m.type) && m.sprite != null)
                spriteByType.Add(m.type, m.sprite);
        }
    }

    private void OnEnable()
    {
        target.OnStatusUpdated += HandleStatusUpdated;
        target.OnEnd += HandleEnd;

        // Show already active effects
        foreach (StatusEffectSystem.StatusType t in Enum.GetValues(typeof(StatusEffectSystem.StatusType)))
        {
            if (target.HasStatus(t))
                CreateOrRefreshIcon(t, target.GetStackCount(t));
        }
    }

    private void OnDisable()
    {
        target.OnStatusUpdated -= HandleStatusUpdated;
        target.OnEnd -= HandleEnd;
    }

    private void Update()
    {
        foreach (var kv in activeIcons)
        {
            var type = kv.Key;
            var activeIcon = kv.Value;

            float remaining = target.GetRemainingTime(type);
            float startDur = activeIcon.startDuration;
            if (startDur <= 0f)
                startDur = Mathf.Max(remaining, 0.0001f);

            float fill = (startDur <= 0.0001f) ? 0f : Mathf.Clamp01(remaining / startDur);
            activeIcon.image.fillAmount = fill;
        }
    }

    private void HandleStatusUpdated(StatusEffectSystem.StatusType type, int stackCount) =>
        CreateOrRefreshIcon(type, stackCount);

    private void HandleEnd(StatusEffectSystem.StatusType type)
    {
        if (activeIcons.TryGetValue(type, out var activeIcon))
        {
            Destroy(activeIcon.image.gameObject);
            activeIcons.Remove(type);
        }
    }

    private void CreateOrRefreshIcon(StatusEffectSystem.StatusType type, int stackCount)
    {
        if (!spriteByType.TryGetValue(type, out var sprite)) return;

        if (activeIcons.TryGetValue(type, out var existing))
        {
            existing.startDuration = Mathf.Max(target.GetRemainingTime(type), 0.0001f);
            existing.image.sprite = sprite;
            UpdateStackText(existing.stackText, stackCount);
            return;
        }

        var newIcon = Instantiate(iconPrefab, iconsParent ? iconsParent : transform);
        newIcon.name = $"StatusIcon_{type}";
        newIcon.sprite = sprite;
        newIcon.fillAmount = 1f;

        TMP_Text stackText = newIcon.GetComponentInChildren<TMP_Text>(true);
        UpdateStackText(stackText, stackCount);

        activeIcons[type] = new ActiveIcon
        {
            image = newIcon,
            stackText = stackText,
            startDuration = Mathf.Max(target.GetRemainingTime(type), 0.0001f)
        };
    }

    private static void UpdateStackText(TMP_Text stackText, int stackCount)
    {
        if (stackText == null) return;

        bool show = stackCount > 1;
        stackText.gameObject.SetActive(show);
        if (show)
            stackText.text = $"x{stackCount}";
    }
}
