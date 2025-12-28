using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class Snappy2DController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonal = true;

    [Header("Feel")]
    [Tooltip("If true: movement is instant (snappy). If false: velocity eases toward target using acceleration. Higher = snappier.")]
    [SerializeField] private bool instant = true;
    [Tooltip("When not instant: how fast velocity moves toward target. Higher = snappier.")]
    [SerializeField] private float acceleration = 50f;

    [Header("UI")]
    [SerializeField] private Slider dashSlider;

    [Header("Dash")]
    [SerializeField] private bool enableDash = true;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Blink")]
    [Tooltip("If true, pressing dash will blink (teleport) instead of dashing.")]
    [SerializeField] private bool dashIsBlink = false;
    [Tooltip("Blink distance when dash is set to blink mode.")]
    [SerializeField] private float blinkDistance = 5f;
    [Tooltip("Layers that block blink. Configure which colliders stop the teleport.")]
    [SerializeField] private LayerMask blinkBlockingLayers = ~0;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("If true, flips the sprite in the opposite direction.")]
    [SerializeField] private bool invertSpriteFlip = false;
    [SerializeField] private AudioClip dashClip;

    private Rigidbody2D rb;
    private Vector2 input;
    private Vector2 targetVelocity;
    private Vector2 velocity; // used when smoothing

    private bool isDashing;
    private float dashEndTime;
    private float nextDashTime;
    private Vector2 dashDirection;
    private AudioSource playerSource;
    private Collider2D bodyCollider;

    public float MoveSpeed => moveSpeed;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown;

    private void Awake()

    {
        playerSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        bodyCollider = GetComponent<Collider2D>();

        if(enableDash == false)
        {
            dashSlider.fillRect.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Handle dash input
        if (!isDashing && Time.time >= nextDashTime && Input.GetKeyDown(KeyCode.Space) && enableDash)
        {
            if (input != Vector2.zero) // dash only if moving
            {
                if (playerSource != null)
                {
                    playerSource.PlayOneShot(dashClip);
                }
                if (dashIsBlink)
                {
                    // Perform blink (teleport) with a safety linecast to avoid blinking into obstacles
                    Vector2 start = rb.position;
                    Vector2 dir = input.normalized;
                    Vector2 end = start + dir * blinkDistance;

                    int mask = blinkBlockingLayers;
                    RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, mask);

                    float maxDist = blinkDistance;
                    if (hits != null && hits.Length > 0)
                    {
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var hit = hits[i];
                            if (hit.collider == null) continue;
                            if (bodyCollider != null && hit.collider == bodyCollider) continue; // ignore self
                            float allowed = hit.distance - 0.05f; // small skin so we don't end up inside the collider
                            if (allowed < 0f) allowed = 0f;
                            maxDist = Mathf.Min(maxDist, allowed);
                            break; // hits are sorted by distance; first valid is our stop
                        }
                    }

                    Vector2 finalPos = start + dir * maxDist;
                    rb.position = finalPos;
                    rb.linearVelocity = Vector2.zero;
                    nextDashTime = Time.time + dashCooldown;
                }
                else
                {
                    isDashing = true;
                    dashDirection = input.normalized;
                    dashEndTime = Time.time + dashDuration;
                    nextDashTime = Time.time + dashCooldown;
                }
            }
        }

        // Movement input
        if (!isDashing)
        {
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");

            if (!allowDiagonal)
            {
                if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                    input.y = 0f;
                else
                    input.x = 0f;
            }

            input = input.normalized;
            targetVelocity = input * moveSpeed;
        }

        // Flip sprite based on horizontal movement
        if (spriteRenderer != null && input.x != 0)
        {
            bool flip = input.x < 0;
            if (invertSpriteFlip) flip = !flip;
            spriteRenderer.flipX = flip;
        }
    }

    public void IncreaseMoveSpeed(float amount)
    {
        if (amount == 0f) return;
        moveSpeed = Mathf.Max(0f, moveSpeed + amount);
    }

    public void IncreaseDashSpeed(float amount)
    {
        if (amount == 0f) return;
        dashSpeed = Mathf.Max(0f, dashSpeed + amount);
    }

    public void IncreaseDashCooldown(float amount)
    {
        if (amount == 0f) return;
        dashCooldown = Mathf.Max(0f, dashCooldown + amount);
    }

    // UnityEvent-compatible toggles for blink mode
    public void EnableBlink()
    {
        dashIsBlink = true;
    }

    public void DisableBlink()
    {
        dashIsBlink = false;
    }

    public void SetBlinkMode(bool enabled)
    {
        dashIsBlink = enabled;
    }

    private void FixedUpdate()
    {
        if (dashSlider != null)
        {
            float remaining = Mathf.Max(0f, nextDashTime - Time.time);
            float fill = 1f - Mathf.Clamp01(remaining / dashCooldown); // 1 = ready

            if (fill >= 1f)
                dashSlider.value = 0f; // ready → empty
            else
                dashSlider.value = fill; // cooling down → fill growing
        }

        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            if (Time.time >= dashEndTime)
                isDashing = false;
            return;
        }

        // First, determine the intended velocity based on input and movement mode
        Vector2 intendedVelocity;
        if (instant)
        {
            intendedVelocity = targetVelocity;
        }
        else
        {
            // For smoothed movement, we update the 'velocity' field
            velocity = Vector2.MoveTowards(velocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            intendedVelocity = velocity;
        }

        // Now, apply status effects to the intended velocity
        if (TryGetComponent<StatusEffectSystem>(out StatusEffectSystem ses))
        {
            // If stunned OR frozen, force velocity to zero
            if (ses.HasStatus(StatusEffectSystem.StatusType.Stun) ||
                ses.HasStatus(StatusEffectSystem.StatusType.Frozen))
            {
                intendedVelocity = Vector2.zero;
            }
            // If hasted, multiply velocity
            else if (ses.HasStatus(StatusEffectSystem.StatusType.Speed))
            {
                intendedVelocity *= 2;
            }
        }

        // Finally, apply the calculated velocity to the rigidbody
        rb.linearVelocity = intendedVelocity;
    }
}
