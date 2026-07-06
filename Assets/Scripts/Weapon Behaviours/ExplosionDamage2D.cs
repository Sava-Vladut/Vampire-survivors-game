using System.Collections.Generic;
using UnityEngine;

public class ExplosionDamage2D : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] public float radius = 2f;
    [SerializeField] public int baseDamage = 20;
    [SerializeField] private bool useDistanceFalloff = true;
    [SerializeField, Min(0f)] private float knockbackForce = 0f;

    [Header("Visuals")]
    [Tooltip("Scale the first child GameObject so it visually matches the radius.")]
    [SerializeField] private bool scaleChildToRadius = true;

    [Header("Filters")]
    [Tooltip("Only objects on these layers will be damaged.")]
    [SerializeField] private LayerMask damageLayers = ~0;

    [Header("Timing")]
    [Tooltip("Trigger explosion automatically on Awake.")]
    [SerializeField] private bool explodeOnAwake = true;
    [Tooltip("Auto-destroy this GameObject after explosion (seconds). 0 = immediate destroy, <0 = don't destroy.")]
    [SerializeField] private float destroyAfter = 0.0f;

    private readonly HashSet<SimpleHealth> _hitOnce = new();

    private void Awake()
    {
        if (scaleChildToRadius && transform.childCount > 0)
        {
            float diameter = radius;
            transform.GetChild(0).localScale = new Vector3(diameter, diameter, 1f);
        }

        if (explodeOnAwake)
        {
            DoExplosion();
            Cleanup();
        }
    }

    /// <summary>Triggers the explosion manually if explodeOnAwake is false.</summary>
    public void DoExplosion()
    {

        Vector2 center = transform.position;
        var cols = Physics2D.OverlapCircleAll(center, radius, damageLayers);
        foreach (var col in cols)
        {
            if (col == null) continue;

            var health = col.GetComponentInParent<SimpleHealth>();
            if (health == null || !health.IsAlive) continue;
            if (_hitOnce.Contains(health)) continue;

            int dmg = CalculateDamage(center, col);
            if (dmg > 0)
            {
                health.TakeDamage(dmg, SimpleHealth.DamageType.Physical);
                ApplyKnockback(center, col);
                _hitOnce.Add(health);
            }
        }
    }

    private int CalculateDamage(Vector2 center, Collider2D col)
    {
        if (!useDistanceFalloff) return baseDamage;

        Vector2 closest = col.ClosestPoint(center);
        float dist = Vector2.Distance(center, closest);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, radius));

        float scaled = baseDamage * (1f - t);
        return Mathf.CeilToInt(scaled);
    }

    private void ApplyKnockback(Vector2 center, Collider2D col)
    {
        if (knockbackForce <= 0f) return;

        Vector2 closestPoint = col.ClosestPoint(center);
        Vector2 direction = closestPoint - center;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = (Vector2)col.bounds.center - center;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        Vector2 impulse = direction.normalized * knockbackForce;
        if (col.GetComponentInParent<EnemyChaser>() is EnemyChaser chaser)
        {
            chaser.ApplyKnockback(impulse);
            return;
        }

        if (col.GetComponentInParent<Snappy2DController>() is Snappy2DController playerMovement)
            playerMovement.ApplyKnockback(impulse);
    }

    private void Cleanup()
    {
        if (destroyAfter < 0f) return;

        if (destroyAfter == 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, destroyAfter);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
