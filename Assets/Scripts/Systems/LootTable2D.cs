using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public class LootTable2D : MonoBehaviour
{
    [System.Serializable]
    public class LootEntry
    {
        [Header("Loot")]
        [Tooltip("Prefab to spawn if this entry is selected.")]
        [ShowAssetPreview] public GameObject prefab;

        [Min(0f)]
        [Tooltip("Relative weight. 0 = never selected. Higher = more likely.")]
        public float weight = 1f;

        // Amount options are now global on LootTable2D
    }

    [System.Serializable]
    public class AmountOption
    {
        [Min(1)]
        [Tooltip("How many to drop when this option is chosen.")]
        public int amount = 1;

        [Min(0f)]
        [Tooltip("Relative chance of choosing this amount.")]
        public float weight = 1f;
    }

    [Header("Loot Table")]
    [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

    [Header("Amount (Weighted)")]
    [Tooltip("Global weighted options for how many to drop when an entry wins.")]
    public List<AmountOption> amountOptions = new()
    {
        new AmountOption(){ amount = 1, weight = 1f }
    };
    [Tooltip("Multiplier applied after the weighted amount is picked. Bosses can raise this at runtime.")]
    [SerializeField, Min(1)] private int dropMultiplier = 1;

    [Header("Roll Settings")]
    [SerializeField] private bool rollOnAwake = true;
    [Tooltip("Optional fixed random seed for deterministic rolls. Leave 0 for Unity's RNG.")]
    [SerializeField] private int seed = 0;

    [Header("Spawn Settings")]
    [Tooltip("Where to spawn. If null, uses this transform.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Z position for spawned object. If 'Use Spawn Point Z' is true, this is ignored.")]
    [SerializeField] private float zPosition = 0f;
    [Tooltip("If true, keep the Z position of the spawn point (or this transform).")]
    [SerializeField] private bool useSpawnPointZ = true;
    [Tooltip("Parent for the spawned object. If null, none.")]
    [SerializeField] private Transform parent;

    [Header("Circle Spawn (2D)")]
    [Tooltip("If true, spawn inside a circle using Random.insideUnitCircle.")]
    [SerializeField] private bool useCircleSpawn = false;
    [Tooltip("Radius of the 2D circle area to spawn within.")]
    [Min(0f)][SerializeField] private float circleRadius = 1f;
    [Tooltip("Optional center for the circle. If null, uses spawnPoint (or this transform).")]
    [SerializeField] private Transform circleCenter;

    [Header("Debug")]
    [ReadOnly][SerializeField] private int lastSelectedIndex = -1;
    [ReadOnly][SerializeField] private GameObject lastSpawned;

    private System.Random seededRng;

    private void Awake()
    {
        if (seed != 0)
            seededRng = new System.Random(seed);

        if (rollOnAwake)
            RollAndSpawn();
    }

    /// <summary>
    /// Rolls amount, then picks and spawns that many entries (duplicates allowed). Returns last spawned, or null.
    /// </summary>
    public GameObject RollAndSpawn()
    {
        int count = PickAmount() * Mathf.Max(1, dropMultiplier);
        if (count <= 0)
        {
            Debug.LogWarning($"[LootTable2D] Picked non-positive amount ({count}) on {name}. Nothing spawned.");
            return null;
        }

        // Base spawn transform
        var sp = spawnPoint != null ? spawnPoint : transform;
        var centerT = circleCenter != null ? circleCenter : sp;

        GameObject last = null;

        for (int i = 0; i < count; i++)
        {
            int idx = WeightedPickIndex(entries);
            if (idx < 0)
            {
                Debug.LogWarning($"[LootTable2D] No valid entries to roll on {name}. Nothing spawned.");
                break;
            }
            var entry = entries[idx];
            Vector3 pos = centerT.position;
            if (useCircleSpawn && circleRadius > 0f)
            {
                Vector2 offset2D = Random.insideUnitCircle * circleRadius;
                pos.x += offset2D.x; pos.y += offset2D.y;
            }
            if (!useSpawnPointZ) pos.z = zPosition;
            last = Instantiate(entry.prefab, pos, Quaternion.identity, parent);
            lastSelectedIndex = idx;
        }

        lastSpawned = last;
        return lastSpawned;
    }

    public void SetDropMultiplier(int multiplier)
    {
        dropMultiplier = Mathf.Max(1, multiplier);
    }

    /// <summary>
    /// Picks an index from a list of LootEntry, respecting entry weight and null/zero guards.
    /// </summary>
    public int WeightedPickIndex(List<LootEntry> list)
    {
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.prefab != null && e.weight > 0f)
                total += e.weight;
        }

        if (total <= 0f) return -1;

        float r = NextFloat() * total;
        float cumulative = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.prefab == null || e.weight <= 0f) continue;

            cumulative += e.weight;
            if (r < cumulative)
                return i;
        }

        // Fallback: last valid
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e.prefab != null && e.weight > 0f)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Picks an amount from the global weighted amount options.
    /// </summary>
    private int PickAmount()
    {
        var opts = amountOptions;
        if (opts == null || opts.Count == 0)
            return 1; // sensible default

        float total = 0f;
        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            if (o.amount >= 1 && o.weight > 0f)
                total += o.weight;
        }

        if (total <= 0f) return 1; // default when all invalid

        float r = NextFloat() * total;
        float cum = 0f;

        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            if (o.amount < 1 || o.weight <= 0f) continue;

            cum += o.weight;
            if (r < cum)
                return Mathf.Max(1, o.amount);
        }

        // Fallback: last valid
        for (int i = opts.Count - 1; i >= 0; i--)
        {
            var o = opts[i];
            if (o.amount >= 1 && o.weight > 0f)
                return o.amount;
        }

        return 1;
    }

    [ContextMenu("DEBUG ▸ Roll (No Spawn)")]
    public void DebugRollOnly()
    {
        lastSelectedIndex = WeightedPickIndex(entries);
        Debug.Log($"[LootTable2D] Rolled index: {lastSelectedIndex} on {name}");
    }

    [ContextMenu("DEBUG ▸ Roll & Spawn")]
    public void DebugRollAndSpawn()
    {
        var go = RollAndSpawn();
        // Debug.Log($"[LootTable2D] Spawned: {(go ? go.name : "null")} on {name}");
    }

    private float NextFloat()
    {
        if (seededRng == null) return Random.value;
        return (float)seededRng.NextDouble();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize circle spawn in Scene view
        if (!useCircleSpawn) return;

        var sp = spawnPoint != null ? spawnPoint : transform;
        var centerT = circleCenter != null ? circleCenter : sp;

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
        Gizmos.DrawSphere(centerT.position, 0.05f);
        Handles.color = new Color(0f, 0.6f, 1f, 0.9f);
        Handles.DrawWireDisc(centerT.position, Vector3.forward, circleRadius);
    }

    private class ReadOnlyInspectorAttribute : PropertyAttribute { }
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
    private class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => UnityEditor.EditorGUI.GetPropertyHeight(property, label, true);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool prev = GUI.enabled; GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = prev;
        }
    }
#endif
}
