using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// After damaging a target, re-aim this Rigidbody2D toward the next damageable target.
/// Works alongside BulletDamageTrigger (keep penetration high enough to allow chaining).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RB2DChainToTag : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Preferred target tag. Used to break ties, but layer + SimpleHealth decide eligibility.")]
    [SerializeField] private string targetTag = "Enemy";
    [Tooltip("Max number of retargets after the first hit.")]
    [SerializeField] public int maxChains = 3;
    [Tooltip("Maximum search radius for next target (0 = unlimited).")]
    [SerializeField] private float searchRadius = 25f;

    [Header("Motion")]
    [Tooltip("If <= 0, reuse current speed. Otherwise, force this travel speed.")]
    [SerializeField] private float travelSpeed = 0f;
    [Tooltip("Optional turn smoothing (0 = instant snap). Retargets happen once per hit, so instant is the reliable default.")]
    [Range(0f, 30f)][SerializeField] private float turnLerp = 0f;

    [Header("Filters")]
    [Tooltip("Layers considered valid chain targets.")]
    [SerializeField] private LayerMask targetLayers = 1 << 7;
    [Tooltip("Ignore the same target twice.")]
    [SerializeField] private bool avoidRepeatTargets = true;

    [Header("Timing")]
    [Tooltip("Small delay before retargeting to let the hit finish (seconds).")]
    [SerializeField] private float retargetDelay = 0.02f;

    private readonly HashSet<Transform> _visited = new();
    private Rigidbody2D _rb;
    private int _chainsDone = 0;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true; // this script expects trigger bullets
    }

    private void OnEnable()
    {
        _chainsDone = 0;
        _visited.Clear();
    }

    // We listen for the same trigger event your BulletDamageTrigger uses.
    // When we touch something that looks like a damageable "enemy", we mark it visited
    // and try to find the next target.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_chainsDone >= maxChains) return;

        // Is it a valid damageable root (has SimpleHealth and is alive)?
        var health = other.GetComponentInParent<SimpleHealth>();
        if (health == null || !health.IsAlive) return;

        if (!IsAllowedTarget(health, other)) return;

        // Record this hit target to avoid selecting it as "next"
        if (avoidRepeatTargets)
            _visited.Add(health.transform);

        // Kick off a retarget (slight delay so BulletDamageTrigger can process)
        if (isActiveAndEnabled)
            Invoke(nameof(DoRetarget), retargetDelay);
    }

    private void DoRetarget()
    {
        if (_chainsDone >= maxChains) return;

        Transform next = FindNextTarget();
        if (next == null) return;

        // Compute desired direction & speed
        Vector2 dir = ((Vector2)next.position - _rb.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        float speed = (travelSpeed > 0f) ? travelSpeed : _rb.linearVelocity.magnitude;
        if (speed <= 0f) speed = 10f; // sane default if bullet was stationary

        if (turnLerp <= 0f)
        {
            // Instant snap toward the next target
            _rb.linearVelocity = dir * speed;
        }
        else
        {
            // Smoothly steer current velocity toward target direction.
            Vector2 desired = dir * speed;
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, desired, Mathf.Clamp01(turnLerp));
        }

        _chainsDone++;
    }

    private Transform FindNextTarget()
    {
        SimpleHealth[] candidates = FindObjectsByType<SimpleHealth>();
        Transform best = null;
        bool bestMatchesPreferredTag = false;
        float bestSqr = float.PositiveInfinity;

        Vector2 p = _rb.position;
        float maxSqr = (searchRadius <= 0f) ? float.PositiveInfinity : searchRadius * searchRadius;

        foreach (var h in candidates)
        {
            if (h == null || !h.IsAlive) continue;
            if (!IsAllowedTarget(h, null)) continue;

            // Skip already visited (hit) targets
            if (avoidRepeatTargets && _visited.Contains(h.transform)) continue;

            float sqr = ((Vector2)h.transform.position - p).sqrMagnitude;
            if (sqr > maxSqr) continue;

            bool matchesPreferredTag = MatchesPreferredTag(h);
            if ((matchesPreferredTag && !bestMatchesPreferredTag) ||
                (matchesPreferredTag == bestMatchesPreferredTag && sqr < bestSqr))
            {
                bestSqr = sqr;
                bestMatchesPreferredTag = matchesPreferredTag;
                best = h.transform;
            }
        }

        return best;
    }

    private bool IsAllowedTarget(SimpleHealth health, Collider2D hitCollider)
    {
        if (health == null || targetLayers.value == 0) return false;

        if (hitCollider != null && IsInTargetLayers(hitCollider.gameObject))
            return true;

        if (IsInTargetLayers(health.gameObject))
            return true;

        var colliders = health.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && IsInTargetLayers(colliders[i].gameObject))
                return true;
        }

        return false;
    }

    private bool IsInTargetLayers(GameObject candidate)
    {
        return candidate != null && (targetLayers.value & (1 << candidate.layer)) != 0;
    }

    private bool MatchesPreferredTag(SimpleHealth health)
    {
        if (health == null || string.IsNullOrWhiteSpace(targetTag)) return false;
        if (health.CompareTag(targetTag)) return true;

        var transforms = health.GetComponentsInChildren<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].CompareTag(targetTag))
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (searchRadius > 0f)
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, searchRadius);
        }
    }
#endif
}
