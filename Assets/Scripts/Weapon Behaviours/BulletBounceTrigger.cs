using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BulletBounceTrigger : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Tooltip("Layers considered walls/obstacles to bounce off.")]
    [SerializeField] private LayerMask wallLayers;
    [Tooltip("How many times the projectile can bounce.")]
    [SerializeField] private int maxBounces = 3;
    [Tooltip("Velocity multiplier after each bounce.")]
    [Range(0f, 1.5f)][SerializeField] private float bounceDamping = 1f;

    private Rigidbody2D _rb;
    private int _remainingBounces;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _remainingBounces = Mathf.Max(0, maxBounces);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((wallLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (_remainingBounces <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Approximate contact normal for triggers using closest point
        Vector2 cp;
        try { cp = other.ClosestPoint(transform.position); }
        catch { cp = _rb.position; }

        Vector2 toSelf = (Vector2)transform.position - cp;
        Vector2 normal = toSelf.sqrMagnitude > 0.000001f ? toSelf.normalized : -_rb.linearVelocity.normalized;

        Vector2 reflected = Vector2.Reflect(_rb.linearVelocity, normal) * Mathf.Max(0f, bounceDamping);
        _rb.linearVelocity = reflected;

        if (reflected.sqrMagnitude > 0.0001f)
        {
            float rotDeg = Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, rotDeg);
        }

        _remainingBounces--;
    }
}
