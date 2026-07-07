using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StatusEffectZoneTrigger2D : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only objects on these layers can receive the zone status.")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [Tooltip("If set, the target root must have this tag.")]
    [SerializeField] private string requiredRootTag = "";
    [Tooltip("If true, the target root must be tagged Player.")]
    [SerializeField] private bool playerOnly = false;

    [Header("Status")]
    [SerializeField] private StatusEffectSystem.StatusType statusEffect = StatusEffectSystem.StatusType.Slow;
    [SerializeField, Min(0.01f)] private float duration = 0.5f;
    [SerializeField, Min(0f)] private float tickInterval = 1f;
    [Tooltip("How often the zone refreshes the status while the target stays inside.")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    private readonly Dictionary<StatusEffectSystem, float> targets = new Dictionary<StatusEffectSystem, float>();
    private readonly List<StatusEffectSystem> targetKeys = new List<StatusEffectSystem>();
    private readonly List<StatusEffectSystem> cleanup = new List<StatusEffectSystem>();

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetStatusTarget(other, out StatusEffectSystem statusSystem))
            return;

        Apply(statusSystem);
        targets[statusSystem] = 0f;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!TryGetStatusTarget(other, out StatusEffectSystem statusSystem))
            return;

        if (!targets.ContainsKey(statusSystem))
            targets.Add(statusSystem, 0f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetStatusTarget(other, out StatusEffectSystem statusSystem))
            return;

        targets.Remove(statusSystem);
    }

    private void Update()
    {
        if (targets.Count == 0)
            return;

        targetKeys.Clear();
        targetKeys.AddRange(targets.Keys);
        cleanup.Clear();

        foreach (StatusEffectSystem statusSystem in targetKeys)
        {
            if (statusSystem == null || !statusSystem.isActiveAndEnabled)
            {
                cleanup.Add(statusSystem);
                continue;
            }

            float nextRefresh = targets[statusSystem] - Time.deltaTime;
            if (nextRefresh <= 0f)
            {
                Apply(statusSystem);
                nextRefresh = refreshInterval;
            }

            targets[statusSystem] = nextRefresh;
        }

        for (int i = 0; i < cleanup.Count; i++)
            targets.Remove(cleanup[i]);
    }

    private void OnDisable()
    {
        targets.Clear();
        targetKeys.Clear();
        cleanup.Clear();
    }

    private void Apply(StatusEffectSystem statusSystem)
    {
        statusSystem.AddStatus(statusEffect, duration, tickInterval, gameObject);
    }

    private bool TryGetStatusTarget(Collider2D other, out StatusEffectSystem statusSystem)
    {
        statusSystem = null;
        if (other == null || !IsInLayerMask(other.gameObject, targetLayers))
            return false;

        Transform root = ResolveTargetRoot(other);
        if (root == null)
            return false;

        string tagFilter = playerOnly ? "Player" : requiredRootTag;
        if (!string.IsNullOrWhiteSpace(tagFilter) && !root.CompareTag(tagFilter))
            return false;

        if (root.TryGetComponent(out statusSystem))
            return true;

        statusSystem = other.GetComponentInParent<StatusEffectSystem>();
        if (statusSystem != null)
            return true;

        return other.TryGetComponent(out statusSystem);
    }

    private static Transform ResolveTargetRoot(Collider2D other)
    {
        if (other.TryGetComponent(out StatusEffectSystem statusSystem))
            return statusSystem.transform;

        statusSystem = other.GetComponentInParent<StatusEffectSystem>();
        if (statusSystem != null)
            return statusSystem.transform;

        if (other.TryGetComponent(out SimpleHealth health))
            return health.transform;

        health = other.GetComponentInParent<SimpleHealth>();
        if (health != null)
            return health.transform;

        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.transform;

        return other.transform.root;
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }
}
