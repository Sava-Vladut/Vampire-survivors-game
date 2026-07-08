// BulletDamageTrigger.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BulletDamageTrigger : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] public SimpleHealth.DamageType damageType;
    [SerializeField] public int damageAmount = 10;
    [HideInInspector] public GameObject sourceObject;
    [Tooltip("How many successful damage hits this bullet can apply before it is destroyed.")]
    [SerializeField] public int penetration = 1;
    [Min(0f)] public float knockbackForce = 0f;
    [FormerlySerializedAs("executeChance")]
    [Tooltip("Enemies at or below this fraction of their max health are instantly slain by this bullet.")]
    [Range(0f, 1f)] public float cullThreshold = 0f;

    [Header("Filters")]
    [Tooltip("Only objects on these layers will be damaged.")]
    [SerializeField] private LayerMask damageLayers = ~0;
    [Tooltip("If the bullet touches any of these layers, it is destroyed immediately (e.g., walls/obstacles).")]
    [SerializeField] private LayerMask destroyOnTouchLayers;
    [SerializeField] private bool allowMultipleHits = false;

    [Header("On Hit Effects")]
    public bool applyStatusEffectOnHit = false;
    public float statusApplyChance = 1f; // optional: chance to apply on hit (0..1)
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    [Tooltip("Duration in seconds for the applied status effect.")]
    public float statusEffectDuration = 3f;
    [HideInInspector] public bool canTriggerForkShot = true;


    [Header("Impact VFX")]
    [Tooltip("Prefab to spawn at the impact point (both on block-hit and on damage).")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("Also spawn impact when hitting a blocking layer.")]
    [SerializeField] private bool spawnOnBlockedHit = true;
    [Tooltip("Spawn impact when damaging a target.")]
    [SerializeField] private bool spawnOnDamageHit = true;

    // Track which healths we already hit (avoid duplicate damage on multi-collider targets)
    private readonly HashSet<SimpleHealth> _alreadyHit = new();

    private Collider2D _col;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col != null) _col.isTrigger = true;

        // IMPORTANT: Don't touch impactPrefab's components here.
        // Shooter sets 'damageAmount' (incl. crit) AFTER Instantiate.
        // We will copy 'damageAmount' to the *spawned impact instance* at spawn time.
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If it hits a blocked layer -> spawn impact + destroy immediately
        if (IsInLayerMask(other.gameObject, destroyOnTouchLayers))
        {
            if (spawnOnBlockedHit) SpawnImpactAt(other, transform.position);
            Destroy(gameObject);
            return;
        }

        // Only damage allowed layers
        if (!IsInLayerMask(other.gameObject, damageLayers)) return;

        // Support child colliders by searching up
        var health = other.GetComponentInParent<SimpleHealth>();
        if (health == null || !health.IsAlive) return;

        if (!allowMultipleHits)
        {
            if (_alreadyHit.Contains(health)) return;
        }
        // Already hit this target? skip


        // Apply damage
        bool cull = cullThreshold > 0f && health.CurrentHealth <= health.MaxHealth * cullThreshold;
        if (cull)
            health.TakeDamage(health.CurrentHealth, damageType, false, false, GetDamageSource(), "Cull");
        else
            health.TakeDamage(damageAmount, damageType, true, true, GetDamageSource(), "Projectile");

        if (knockbackForce > 0f)
            ApplyKnockback(other);

        TryTriggerForkShot(other);

        if (!allowMultipleHits)
        {
            _alreadyHit.Add(health);
        }


        // ---- Apply status effect (if enabled and target supports it) ----
        if (applyStatusEffectOnHit && statusEffectDuration > 0f)
        {
            var statusSys = other.GetComponentInParent<StatusEffectSystem>();
            if (statusSys != null)
            {
                if (Random.Range(0f, 1f) <= statusApplyChance)
                {
                    statusSys.AddStatus(statusEffectOnHit, statusEffectDuration, 1f, GetDamageSource());
                }
                // This will refresh if the same status already exists (per your StatusEffectSystem)

            }
        }

        if (spawnOnDamageHit) SpawnImpactAt(other, transform.position);

        // Consume penetration and destroy if spent
        penetration--;
        if (penetration <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void SpawnImpactAt(Collider2D other, Vector3 fallback)
    {
        if (impactPrefab == null) return;

        // Best-effort contact point for triggers
        Vector3 hitPos = GetImpactPosition(other, fallback);

        // Instantiate the impact and copy the *current* bullet damage to it (if it has ExplosionDamage2D)
        var impactInstance = Instantiate(impactPrefab, hitPos, Quaternion.identity);
        if (impactInstance.TryGetComponent<ExplosionDamage2D>(out var explosionInstance))
        {
            // Use current damageAmount (already includes crits if SimpleShooter set it)
            explosionInstance.baseDamage = damageAmount;
            explosionInstance.damageType = damageType;
            explosionInstance.sourceObject = GetDamageSource();
            explosionInstance.sourceDetail = "Projectile Explosion";
        }
    }

    private void TryTriggerForkShot(Collider2D other)
    {
        if (!canTriggerForkShot)
            return;

        GameObject damageSource = GetDamageSource();
        SimpleShooter shooter = damageSource != null ? damageSource.GetComponentInParent<SimpleShooter>() : null;
        if (shooter == null)
            return;

        shooter.TrySpawnForkOnHit(GetImpactPosition(other, transform.position), GetTravelDirection(), damageAmount);
    }

    private Vector3 GetImpactPosition(Collider2D other, Vector3 fallback)
    {
        try
        {
            Vector2 cp = other.ClosestPoint(transform.position);
            return new Vector3(cp.x, cp.y, fallback.z);
        }
        catch
        {
            return fallback;
        }
    }

    private Vector2 GetTravelDirection()
    {
        if (TryGetComponent(out Rigidbody2D rb) && rb.linearVelocity.sqrMagnitude > 0.0001f)
            return rb.linearVelocity.normalized;

        return transform.right;
    }

    private GameObject GetDamageSource()
    {
        return sourceObject != null ? sourceObject : gameObject;
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }

    private void ApplyKnockback(Collider2D other)
    {
        Vector2 impulse = GetKnockbackDirection(other) * knockbackForce;

        if (other.GetComponentInParent<EnemyChaser>() is EnemyChaser chaser)
        {
            chaser.ApplyKnockback(impulse);
            return;
        }

        if (other.GetComponentInParent<Snappy2DController>() is Snappy2DController playerMovement)
            playerMovement.ApplyKnockback(impulse);
    }

    private Vector2 GetKnockbackDirection(Collider2D other)
    {
        if (TryGetComponent(out Rigidbody2D rb) && rb.linearVelocity.sqrMagnitude > 0.0001f)
            return rb.linearVelocity.normalized;

        Vector2 awayFromSource = (Vector2)other.bounds.center - (Vector2)transform.position;
        if (awayFromSource.sqrMagnitude > 0.0001f)
            return awayFromSource.normalized;

        return transform.right;
    }
}
