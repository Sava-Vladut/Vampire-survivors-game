using System.Collections.Generic;
using UnityEngine;

public class ExplosionDamage2D : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField] public float radius = 2f;
    [SerializeField] public int baseDamage = 20;
    [SerializeField] public SimpleHealth.DamageType damageType = SimpleHealth.DamageType.Physical;
    [HideInInspector] public GameObject sourceObject;
    [HideInInspector] public string sourceDetail = "Explosion";
    [HideInInspector] public bool isCritical;
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
    private bool _hasExploded;

    private void Awake()
    {
        if (scaleChildToRadius && transform.childCount > 0)
        {
            float diameter = radius;
            transform.GetChild(0).localScale = new Vector3(diameter, diameter, 1f);
        }

    }

    private void Start()
    {
        // Start runs after the spawning weapon has had a chance to assign damage,
        // source, crit state, and other runtime data to an impact explosion.
        if (explodeOnAwake)
        {
            DoExplosion();
            Cleanup();
        }
    }

    /// <summary>Triggers the explosion manually if explodeOnAwake is false.</summary>
    public void DoExplosion()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Vector2 center = transform.position;
        PlayerAccessoryStats stats = GetSourceAccessoryStats();
        float effectiveRadius = GetEffectiveRadius(stats);
        ScaleVisual(effectiveRadius);
        var cols = Physics2D.OverlapCircleAll(center, effectiveRadius, damageLayers);
        foreach (var col in cols)
        {
            if (col == null) continue;

            var health = col.GetComponentInParent<SimpleHealth>();
            if (health == null || !health.IsAlive) continue;
            if (_hitOnce.Contains(health)) continue;

            int dmg = CalculateDamage(center, col, effectiveRadius, stats);
            if (dmg > 0)
            {
                health.TakeDamage(dmg, damageType, true, true, sourceObject != null ? sourceObject : gameObject, sourceDetail, isCritical);
                ApplyKnockback(center, col, stats);
                _hitOnce.Add(health);
            }
        }
    }

    private int CalculateDamage(Vector2 center, Collider2D col, float effectiveRadius, PlayerAccessoryStats stats)
    {
        int effectiveBaseDamage = sourceObject == null && stats != null ? stats.ApplyGlobalDamage(baseDamage) : baseDamage;
        if (!useDistanceFalloff) return effectiveBaseDamage;

        Vector2 closest = col.ClosestPoint(center);
        float dist = Vector2.Distance(center, closest);
        float t = Mathf.Clamp01(dist / Mathf.Max(0.0001f, effectiveRadius));

        float scaled = effectiveBaseDamage * (1f - t);
        return Mathf.CeilToInt(scaled);
    }

    private void ApplyKnockback(Vector2 center, Collider2D col, PlayerAccessoryStats stats)
    {
        float effectiveKnockback = knockbackForce;
        if (stats != null) effectiveKnockback += stats.KnockbackStrengthBonus;
        if (effectiveKnockback <= 0f) return;

        Vector2 closestPoint = col.ClosestPoint(center);
        Vector2 direction = closestPoint - center;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = (Vector2)col.bounds.center - center;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        Vector2 impulse = direction.normalized * effectiveKnockback;
        if (col.GetComponentInParent<EnemyChaser>() is EnemyChaser chaser)
        {
            chaser.ApplyKnockback(impulse);
            return;
        }

        if (col.GetComponentInParent<Snappy2DController>() is Snappy2DController playerMovement)
            playerMovement.ApplyKnockback(impulse);
    }

    private float GetEffectiveRadius(PlayerAccessoryStats stats)
    {
        return Mathf.Max(0f, radius * (stats != null ? stats.WeaponAreaMultiplier : 1f));
    }

    private PlayerAccessoryStats GetSourceAccessoryStats()
    {
        PlayerAccessoryStats stats = PlayerAccessoryStats.Find(sourceObject != null ? sourceObject.transform : null);
        if (stats != null) return stats;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        bool damagesEnemies = enemyLayer >= 0 && (damageLayers.value & (1 << enemyLayer)) != 0;
        return damagesEnemies ? PlayerAccessoryStats.FindPlayer() : null;
    }

    private void ScaleVisual(float effectiveRadius)
    {
        if (!scaleChildToRadius || transform.childCount <= 0) return;
        float diameter = effectiveRadius;
        Transform visual = transform.GetChild(0);
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        visual.localScale = new Vector3(diameter / parentScaleX, diameter / parentScaleY, 1f);
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
