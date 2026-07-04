using UnityEngine;

[ExecuteAlways]
public class AccessoriesUpgrades : MonoBehaviour
{
    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with AccessoriesUpgrades in the same parent.")]
    public AccessoriesUpgrades nextUpgrade;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        AutoAssignNextUpgrade();   // keep next wired
        EnqueueNextUpgradeOnce();  // push next's Upgrade into chooser once
    }

    private void OnEnable()
    {
        // Editor recompile / domain reload safety
        AutoAssignNextUpgrade();
        EnqueueNextUpgradeOnce();
    }

    private void OnValidate()
    {
        // Auto-set icon from parent's Accessory if available
        if (transform.parent != null && transform.parent.TryGetComponent(out Accessory accessory))
        {
            if (accessory.icon != null && Upgrade != null)
            {
                Upgrade.powerUpIcon = accessory.icon;
            }
        }

        // Keep next wired live in the editor as you reorder children
        AutoAssignNextUpgrade();

    }

    private void OnTransformParentChanged()
    {
        AutoAssignNextUpgrade();
    }

    private void OnTransformChildrenChanged()
    {
        AutoAssignNextUpgrade();
    }

    // ---------------------- Auto-wire NEXT ----------------------

    /// <summary>
    /// Automatically sets nextUpgrade to the next sibling under the same parent
    /// that has an AccessoriesUpgrades component. If the immediate next child
    /// doesn't have it, scans forward until it finds one. Clears if none found.
    /// </summary>
    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = UpgradeChainUtil.FindNextSibling<AccessoriesUpgrades>(transform);

#if UNITY_EDITOR
        if (old != nextUpgrade)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (nextUpgrade == null) return;
        UpgradeChainUtil.EnqueueOnce(UpgradeChainUtil.GetChooser(), nextUpgrade.Upgrade);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(AccessoriesUpgrades))]
    private class AccessoriesUpgradesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var au = (AccessoriesUpgrades)target;
            var so = serializedObject;

            // Always refresh wiring while the inspector is visible
            au.AutoAssignNextUpgrade();

            so.Update();
            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("Upgrade"));

            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.ObjectField(
                    "Next Upgrade (auto)",
                    au.nextUpgrade,
                    typeof(AccessoriesUpgrades),
                    true
                );
            }

            so.ApplyModifiedProperties();
        }
    }
#endif

}