using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject tooltipPanel;          // Parent panel (must be under a Canvas)
    [SerializeField] private TextMeshProUGUI tooltipText;      // Text element inside the panel

    [SerializeField] private Vector2 padding = new Vector2(20f, 10f); // Extra space around text
    [SerializeField] private Vector2 clampMargin = new Vector2(8f, 8f); // Keep inside canvas
    [Header("Positioning")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(10f, 10f);

    [Header("Behaviour")]
    [Tooltip("Hide automatically after losing the target or pointer exits.")]
    [SerializeField] private bool autoHideWhenNoTarget = true;

    [Tooltip("Time (seconds, unscaled) to wait before hiding after losing target.")]
    [SerializeField] private float hideDelay = 1.5f;

    private RectTransform panelRect;
    private Canvas canvas;
    private RectTransform canvasRect;

    // Track the current source and (optional) UI anchor rect
    private TooltipTarget currentTarget;
    private RectTransform currentAnchor;

    private Coroutine hideCoroutine;
    private static CoroutineRunner runner; // ensures coroutines can run even if this object is inactive

    private void Awake()
    {
        // Singleton
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (tooltipPanel == null || tooltipText == null)
        {
            Debug.LogError("[TooltipManager] Assign tooltipPanel and tooltipText in the inspector.");
            enabled = false;
            return;
        }

        panelRect = tooltipPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            Debug.LogError("[TooltipManager] tooltipPanel must be a UI object with RectTransform.");
            enabled = false;
            return;
        }

        // Find the nearest parent canvas; fall back to any canvas if needed (Unity 6 API)
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TooltipManager] No Canvas found. Place TooltipManager under a Canvas.");
            enabled = false;
            return;
        }

        canvasRect = canvas.transform as RectTransform;
        HideTooltipImmediate();

        // Make sure we can receive UI pointer events (Unity 6 API)
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(canvas.transform);
        }
    }

    private void Update()
    {
        if (!tooltipPanel.activeSelf) return;

        // If we lost a valid target, schedule a hide (once)
        if (autoHideWhenNoTarget)
        {
            bool targetAlive = currentTarget != null &&
                               currentTarget.isActiveAndEnabled &&
                               currentTarget.gameObject.activeInHierarchy;

            if (!targetAlive)
            {
                ScheduleHideIfNeeded();
                currentTarget = null;
                currentAnchor = null;
            }
        }

        // Follow anchor (UI) if provided; otherwise follow mouse
        PositionPanel();
    }

    /// <summary>
    /// Show the tooltip. If an anchor is provided (typically a UI RectTransform),
    /// the tooltip follows that anchor. Otherwise it follows the mouse.
    /// </summary>
    public void ShowTooltip(string message, TooltipTarget source = null, RectTransform anchor = null)
    {
        currentTarget = source;
        currentAnchor = anchor;

        CancelScheduledHide();

        if (!tooltipPanel.activeSelf)
            tooltipPanel.SetActive(true);

        tooltipText.text = message;

        // Resize panel to text + padding
        tooltipText.ForceMeshUpdate();
        var newSize = new Vector2(
            tooltipText.preferredWidth + padding.x,
            tooltipText.preferredHeight + padding.y
        );
        panelRect.sizeDelta = newSize;

        // Position immediately
        PositionPanel();
    }

    public void HideTooltip()
    {
        ScheduleHideIfNeeded();
    }

    private void HideTooltipImmediate()
    {
        CancelScheduledHide();
        tooltipPanel.SetActive(false);
        tooltipText.text = string.Empty;
        currentTarget = null;
        currentAnchor = null;
    }

    private void ScheduleHideIfNeeded()
    {
        if (!tooltipPanel.activeSelf) return; // already hidden
        if (hideCoroutine != null) return;    // already scheduled
        if (isActiveAndEnabled)
        {
            hideCoroutine = StartCoroutine(HideAfterDelay());
        }
        else
        {
            hideCoroutine = GetRunner().StartCoroutine(HideAfterDelay());
        }
    }

    private void CancelScheduledHide()
    {
        if (hideCoroutine != null)
        {
            if (isActiveAndEnabled)
                StopCoroutine(hideCoroutine);
            else if (runner != null)
                runner.StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        float t = 0f;
        while (t < hideDelay)
        {
            t += Time.unscaledDeltaTime; // unaffected by timeScale
            yield return null;
        }

        HideTooltipImmediate();
    }

    // --- Positioning ---

    private void PositionPanel()
    {
        if (canvasRect == null) return;

        // 1) Get the screen point for mouse or anchor (NO offset yet)
        Vector2 screenPoint;
        if (currentAnchor != null && currentAnchor.gameObject.activeInHierarchy)
        {
            screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, currentAnchor.position);
        }
        else
        {
            screenPoint = Input.mousePosition;
        }

        // 2) Convert to local point in the canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out var localPoint
        );

        // 3) Apply offset in *UI units* so it scales with the canvas
        //    (Canvas.scaleFactor converts pixels <-> UI units)
        float sf = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        Vector2 offsetInLocalUnits = tooltipOffset / sf;
        localPoint += offsetInLocalUnits;

        // 4) Clamp to canvas bounds
        Vector2 half = panelRect.sizeDelta * 0.5f;
        Vector2 min = (canvasRect.rect.min + half) + clampMargin;
        Vector2 max = (canvasRect.rect.max - half) - clampMargin;

        localPoint.x = Mathf.Clamp(localPoint.x, min.x, max.x);
        localPoint.y = Mathf.Clamp(localPoint.y, min.y, max.y);

        panelRect.localPosition = localPoint;
    }

    // A tiny hidden helper to host coroutines when this object is inactive
    private static CoroutineRunner GetRunner()
    {
        if (runner != null) return runner;
        var go = new GameObject("TooltipCoroutineRunner");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<CoroutineRunner>();
        return runner;
    }

    private class CoroutineRunner : MonoBehaviour { }
}
