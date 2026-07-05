using UnityEngine;
using UnityEngine.UI;

public class InventoryButtonActivator : MonoBehaviour
{
    public enum TargetMode { ButtonInteractable, SetActiveOnGameObject, EnableBehaviour }

    [Header("Inventory Source")]
    [SerializeField] private SimpleInventory inventory;

    [Header("Requirement")]
    [SerializeField] public string requiredItemName = "Orb 1";
    [Min(1)]
    [SerializeField] public int requiredAmount = 1;

    [Header("Target To Toggle")]
    [SerializeField] private TargetMode mode = TargetMode.ButtonInteractable;
    [SerializeField] private Button targetButton;           // used if mode == ButtonInteractable
    [SerializeField] private GameObject targetObject;       // used if mode == SetActiveOnGameObject
    [SerializeField] private Behaviour targetBehaviour;     // used if mode == EnableBehaviour

    [Header("Consume On Click (Optional)")]
    [SerializeField] private bool consumeOnClick = false;
    [Min(1)]
    [SerializeField] private int consumeAmount = 1;

    [Header("Refresh Policy")]
    [Tooltip("If true, reevaluate every frame. Otherwise only on Start/OnEnable and after click.")]
    [SerializeField] private bool continuousRefresh = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Reset()
    {
        // Auto-wire a Button on the same GameObject
        if (!targetButton) targetButton = GetComponent<Button>();
        AutoFindInventory();
    }

    private void Awake()
    {
        AutoFindInventory();
    }

    private void OnEnable()
    {
        // (Re)hook the listener here to cover cases where references were assigned after Awake
        HookButtonListener(true);
        Refresh();
    }

    private void OnDisable()
    {
        HookButtonListener(false);
    }

    private void Update()
    {
        if (continuousRefresh) Refresh();
    }

    public void Refresh()
    {
        if (!inventory)
        {
            ApplyState(false);
            if (debugLogs) Debug.LogWarning("[InventoryButtonActivator] No inventory found.");
            return;
        }

        bool hasEnough = inventory.HasItemAmount(requiredItemName, requiredAmount);
        if (debugLogs) Debug.Log($"[InventoryButtonActivator] Has '{requiredItemName}' x{requiredAmount}? {hasEnough}");
        ApplyState(hasEnough);
    }

    private void ApplyState(bool enabledState)
    {
        switch (mode)
        {
            case TargetMode.ButtonInteractable:
                if (targetButton) targetButton.interactable = enabledState;
                break;

            case TargetMode.SetActiveOnGameObject:
                if (targetObject) targetObject.SetActive(enabledState);
                break;

            case TargetMode.EnableBehaviour:
                if (targetBehaviour) targetBehaviour.enabled = enabledState;
                break;
        }
    }

    private void HookButtonListener(bool hook)
    {
        if (mode != TargetMode.ButtonInteractable) return;
        if (!targetButton) return;

        if (hook)
        {
            targetButton.onClick.RemoveListener(OnClickedConsumeIfNeeded);
            targetButton.onClick.AddListener(OnClickedConsumeIfNeeded);
        }
        else
        {
            targetButton.onClick.RemoveListener(OnClickedConsumeIfNeeded);
        }
    }

    private void OnClickedConsumeIfNeeded()
    {
        if (!consumeOnClick || !inventory) { Refresh(); return; }

        if (debugLogs) Debug.Log($"[InventoryButtonActivator] Click: trying to consume {consumeAmount} x '{requiredItemName}'");

        bool ok = inventory.TryConsume(requiredItemName, consumeAmount);
        if (debugLogs) Debug.Log($"[InventoryButtonActivator] Consume result: {ok}");

        Refresh();
    }

    /// <summary>
    /// Call this from any UI event (e.g., another button) to attempt consumption even if not in ButtonInteractable mode.
    /// </summary>
    public void ConsumeNow()
    {
        if (!inventory) return;
        if (!consumeOnClick) return;

        bool ok = inventory.TryConsume(requiredItemName, consumeAmount);
        if (debugLogs) Debug.Log($"[InventoryButtonActivator] ConsumeNow() => {ok}");
        Refresh();
    }

    /// <summary>Public API: change requirement at runtime.</summary>
    public void SetRequirement(string itemName, int minAmount)
    {
        requiredItemName = itemName;
        requiredAmount = Mathf.Max(1, minAmount);
        Refresh();
    }

    private void AutoFindInventory()
    {
        if (inventory) return;

        // Newer Unity API (faster, no alloc)
#if UNITY_2023_1_OR_NEWER
        inventory = Object.FindAnyObjectByType<SimpleInventory>(FindObjectsInactive.Exclude);
#else
        // Fallback for older versions
        inventory = FindObjectOfType<SimpleInventory>();
#endif
    }
}
