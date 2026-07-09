using UnityEngine;

[AddComponentMenu("AI/Follow Nearest 2D (Ultra Simple)")]
public class FollowNearestOptimized : MonoBehaviour
{
    [Header("Find Targets")]
    public string targetTag = "Target";
    public float searchRadius = 10f;

    [Header("Movement")]
    public float moveSpeed = 5f;

    Transform tr;
    Transform target;

    void Awake() => tr = transform;

    void Update()
    {
        float effectiveRadius = GetEffectiveSearchRadius(target);
        if (target == null || ((Vector2)target.position - (Vector2)tr.position).sqrMagnitude > effectiveRadius * effectiveRadius)
            FindNearest();

        if (target != null)
            tr.position = Vector2.MoveTowards(tr.position, target.position, moveSpeed * Time.deltaTime);
    }

    void FindNearest()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag(targetTag);
        float bestD2 = float.PositiveInfinity;
        Transform best = null;

        foreach (var o in objs)
        {
            if (!o) continue;
            var t = o.transform;
            if (t == tr) continue;

            float d2 = ((Vector2)t.position - (Vector2)tr.position).sqrMagnitude;
            float effectiveRadius = GetEffectiveSearchRadius(t);
            if (d2 < bestD2 && d2 <= effectiveRadius * effectiveRadius)
            {
                bestD2 = d2;
                best = t;
            }
        }
        target = best;
    }

    private float GetEffectiveSearchRadius(Transform candidate)
    {
        if (!string.Equals(targetTag, "Player", System.StringComparison.OrdinalIgnoreCase) || candidate == null)
            return Mathf.Max(0f, searchRadius);

        PlayerAccessoryStats stats = PlayerAccessoryStats.Find(candidate);
        return Mathf.Max(0f, searchRadius + (stats != null ? stats.PickupRadiusBonus : 0f));
    }
}
