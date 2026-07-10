using System.Collections.Generic;
using UnityEngine;

public class RandomTarget2D : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [Tooltip("Optional custom origin. If empty, uses this transform.")]
    [SerializeField] private Transform centerPoint;
    [Header("Enemy Targeting")]
    [Tooltip("Layers searched by MoveToRandomTarget. Defaults to the enemy layer.")]
    [SerializeField] private LayerMask targetMask = 1 << 7;
    [Tooltip("Move to a random point when no living target is in range.")]
    [SerializeField] private bool fallbackToRandomLocation = true;

    private readonly List<Collider2D> targetColliders = new List<Collider2D>(32);
    private readonly List<Collider2D> validTargets = new List<Collider2D>(32);
    private readonly HashSet<SimpleHealth> processedHealth = new HashSet<SimpleHealth>();
    private ContactFilter2D targetFilter;

    /// <summary>
    /// Moves this transform to a random position inside the circle radius.
    /// </summary>
    public void MoveToRandomLocation()
    {
        Vector3 origin = GetCenterPosition();

        // Pick random point inside a circle
        Vector2 randomOffset = Random.insideUnitCircle * radius;
        transform.position = origin + (Vector3)randomOffset;
    }

    /// <summary>
    /// Moves to a random living target in range. This is useful for ground strikes and
    /// other effects that should feel random without repeatedly landing in empty space.
    /// </summary>
    public void MoveToRandomTarget()
    {
        Vector3 origin = GetCenterPosition();
        targetColliders.Clear();
        validTargets.Clear();
        processedHealth.Clear();

        targetFilter.SetLayerMask(targetMask);
        targetFilter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.OverlapCircle(origin, Mathf.Max(0f, radius), targetFilter, targetColliders);

        for (int i = 0; i < targetColliders.Count; i++)
        {
            Collider2D candidate = targetColliders[i];
            if (candidate == null)
                continue;

            SimpleHealth health = candidate.GetComponentInParent<SimpleHealth>();
            if (health == null || !health.IsAlive || health.IsInvulnerable || !processedHealth.Add(health))
                continue;

            // Prefer the collider that owns SimpleHealth because Knife resolves damage
            // on the collider GameObject itself.
            Collider2D damageCollider = candidate.GetComponent<SimpleHealth>() == health
                ? candidate
                : health.GetComponent<Collider2D>();
            if (damageCollider != null && damageCollider.enabled)
                validTargets.Add(damageCollider);
        }

        if (validTargets.Count > 0)
        {
            Collider2D selected = validTargets[Random.Range(0, validTargets.Count)];
            Vector3 targetPosition = selected.bounds.center;
            targetPosition.z = transform.position.z;
            transform.position = targetPosition;
        }
        else if (fallbackToRandomLocation)
        {
            MoveToRandomLocation();
        }
    }

    private Vector3 GetCenterPosition()
    {
        return centerPoint != null ? centerPoint.position : transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = GetCenterPosition();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, radius);
    }
}
