using System.Collections.Generic;
using UnityEngine;

public class SurroundedDamageAccessory : MonoBehaviour, IPlayerDamageMultiplierProvider, IAccessoryDescriptionProvider
{
    [SerializeField, Min(0f)] private float radius = 5f;
    [SerializeField, Tooltip("Damage bonus gained per surrounding enemy. 0.10 = 10%.")]
    private float bonusPerSurroundingEnemy = 0.10f;
    [SerializeField, Tooltip("Maximum bonus damage. 1 = +100%.")]
    private float maxBonusDamage = 1f;
    [SerializeField] private LayerMask enemyLayers = ~0;
    [SerializeField, Min(0.01f)] private float refreshInterval = 0.15f;

    private readonly HashSet<SimpleHealth> nearbyEnemies = new HashSet<SimpleHealth>();
    private int surroundingEnemyCount;
    private int lastDisplayedCount = -1;
    private float nextRefreshTime;

    public float CurrentBonusDamage => Mathf.Min(surroundingEnemyCount * bonusPerSurroundingEnemy, maxBonusDamage);
    public float DamageMultiplier => 1f + CurrentBonusDamage;

    private void OnEnable()
    {
        RefreshCount(true);
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime)
            return;

        RefreshCount();
    }

    private void RefreshCount(bool force = false)
    {
        nextRefreshTime = Time.time + refreshInterval;
        nearbyEnemies.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            SimpleHealth health = hit.GetComponentInParent<SimpleHealth>();
            if (health == null || !health.IsAlive || health.CompareTag("Player"))
                continue;

            if (health.GetComponent<EnemyChaser>() == null)
                continue;

            nearbyEnemies.Add(health);
        }

        surroundingEnemyCount = nearbyEnemies.Count;
        if (force || surroundingEnemyCount != lastDisplayedCount)
        {
            lastDisplayedCount = surroundingEnemyCount;
            GetComponent<Accessory>()?.NotifyRootToRefresh();
        }
    }

    public string GetAccessoryDescriptionLine()
    {
        return $"<color=#FFD166>+{CurrentBonusDamage * 100f:F0}% Damage ({surroundingEnemyCount} nearby enemies)</color>";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
