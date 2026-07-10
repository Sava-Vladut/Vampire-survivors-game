using UnityEngine;
using UnityEngine.Events;

public class GrowOverTime : MonoBehaviour
{
    [Tooltip("How fast the object grows per second.")]
    public float growthRate = 1f;

    [Tooltip("The target scale (uniform) at which to trigger the event.")]
    public float targetScale = 3f;

    [Tooltip("Event to trigger when the target scale is reached or exceeded.")]
    public UnityEvent onTargetReached;

    private bool triggered = false;
    private float areaMultiplier = 1f;

    /// <summary>
    /// Keeps a growing damage area's lifetime unchanged while scaling its physical
    /// size, growth speed, and final size with the owning player's area stat.
    /// </summary>
    public void ConfigureAreaScale(float multiplier)
    {
        areaMultiplier = Mathf.Max(0f, multiplier);
    }


    public void InstantiateExplosion(GameObject explosion)
    {
        GameObject exploder = Instantiate(explosion, transform.position, Quaternion.identity);
        var explosionDamage = exploder.GetComponent<ExplosionDamage2D>();
        explosionDamage.baseDamage = Mathf.RoundToInt(targetScale * 10f);
        explosionDamage.sourceObject = gameObject;
        explosionDamage.sourceDetail = "Growth Explosion";
        explosionDamage.DoExplosion();
    }

    private void Update()
    {
        // Grow uniformly
        transform.localScale += Vector3.one * (growthRate * areaMultiplier * Time.deltaTime);

        // Check if we've reached the target
        float effectiveTargetScale = targetScale * areaMultiplier;
        if (!triggered && transform.localScale.x >= effectiveTargetScale)
        {
            triggered = true;
            onTargetReached?.Invoke();
        }
    }
}
