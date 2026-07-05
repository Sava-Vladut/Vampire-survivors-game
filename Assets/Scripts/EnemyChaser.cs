// EnemyChaser.cs
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("If left empty, will try to find GameObject with tag 'Player' on Awake.")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [Tooltip("Speed in units per second.")]
    [SerializeField] public float moveSpeed = 3f;
    [Tooltip("How close to the target before stopping.")]
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Flee Behavior")]
    [Tooltip("If enabled, enemy flees when inside stoppingDistance, chases when outside.")]
    [SerializeField] private bool enableFlee = false;
    [Tooltip("Dead zone half-width around stoppingDistance where velocity is set to 0 to avoid flip-flopping between chase and flee (only used if flee enabled).")]
    [SerializeField] private float fleeBuffer = 0.25f;

    [Header("Knockback")]
    [Tooltip("How fast knockback velocity fades, in units per second squared. Higher = snappier recovery.")]
    [SerializeField] private float knockbackDecay = 15f;

    [Header("Reach Event")]
    [Tooltip("If true, allows the reach event to fire again after the target moves away far enough.")]
    [SerializeField] private bool repeatEvent = false;
    [Tooltip("Extra distance beyond stoppingDistance the target must exceed to reset the reached state (only used if repeatEvent is true).")]
    [SerializeField] private float resetDistanceBuffer = 1f;
    public UnityEvent onReachDestination;

    private Rigidbody2D rb;
    private StatusEffectSystem cachedStatusEffects;
    private SimpleHealth cachedHealth;
    private bool hasReached;
    private Vector2 knockbackVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        TryGetComponent(out cachedStatusEffects);
        TryGetComponent(out cachedHealth);

        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        if (target == null)
        {
            Debug.LogWarning($"{name}: No target assigned and no GameObject with tag 'Player' was found.");
        }
    }

    public void InstantiateExplosion(GameObject explosion)
    {
        GameObject exploder = Instantiate(explosion, transform.position, Quaternion.identity);
        var explosionComp = exploder.GetComponent<ExplosionDamage2D>();
        if (explosionComp != null)
        {
            int maxHp = cachedHealth != null ? cachedHealth.maxHealth : (TryGetComponent(out cachedHealth) ? cachedHealth.maxHealth : 0);
            explosionComp.baseDamage = maxHp / 3;
            explosionComp.DoExplosion();
        }
    }

    // Adds an instantaneous push (units/sec) that decays over time via knockbackDecay.
    public void ApplyKnockback(Vector2 impulse)
    {
        knockbackVelocity += impulse;
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        float distSqr = toTarget.sqrMagnitude;

        // Decide desired direction (flee/approach with deadband) using squared distances
        Vector2 desiredDir = Vector2.zero;
        bool needsMove = false;
        if (enableFlee)
        {
            float buffer = Mathf.Max(0f, fleeBuffer);
            float lower = Mathf.Max(0f, stoppingDistance - buffer);
            float upper = stoppingDistance + buffer;
            float lowerSqr = lower * lower;
            float upperSqr = upper * upper;

            if (distSqr < lowerSqr)
            {
                needsMove = true;
                float distance = Mathf.Sqrt(distSqr);
                if (distance > 1e-6f) desiredDir = (-toTarget) / distance; // flee
            }
            else if (distSqr > upperSqr)
            {
                needsMove = true;
                float distance = Mathf.Sqrt(distSqr);
                if (distance > 1e-6f) desiredDir = toTarget / distance; // chase
            }
        }
        else
        {
            float stopSqr = stoppingDistance * stoppingDistance;
            if (distSqr > stopSqr)
            {
                needsMove = true;
                float distance = Mathf.Sqrt(distSqr);
                if (distance > 1e-6f) desiredDir = toTarget / distance;
            }
        }

        // Fire reach event on first entry
        if (distSqr <= stoppingDistance * stoppingDistance && !hasReached)
        {
            hasReached = true;
            onReachDestination?.Invoke();
        }

        // Movement multiplier from status effects (cached component)
        float mult = GetMoveMultiplier(cachedStatusEffects); // 0 if Stun/Frozen, 2 if Speed, else 1

        // Apply velocity (knockback is blended in on top of chase movement)
        float speed = moveSpeed * mult;
        rb.linearVelocity = (needsMove ? (desiredDir * speed) : Vector2.zero) + knockbackVelocity;
        knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);

        ResetReachedIfFar(distSqr);
    }

    private void ResetReachedIfFar(float distSqr)
    {
        if (!hasReached || !repeatEvent) return;
        float resetRadius = stoppingDistance + resetDistanceBuffer;
        if (distSqr > resetRadius * resetRadius) hasReached = false;
    }

    private float GetMoveMultiplier(StatusEffectSystem ses)
    {
        // If there's no StatusEffectSystem on this GameObject, treat it as having no statuses.
        if (ses == null)
            return 1f;

        if (ses.HasStatus(StatusEffectSystem.StatusType.Stun) || ses.HasStatus(StatusEffectSystem.StatusType.Frozen))
            return 0f;

        float m = 1f;
        if (ses.HasStatus(StatusEffectSystem.StatusType.Speed)) m *= 2f;   // tweak in inspector later
        return m;
    }

}
