using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public class SafeZoneTrigger2D : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only objects on these layers can activate the safe zone.")]
    [SerializeField] private LayerMask playerLayers = 1 << 6;
    [Tooltip("If set, the entering root must have this tag.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Safe Zone")]
    [Tooltip("How far from the safe player enemies should be frozen.")]
    [FormerlySerializedAs("enemyFleeRadius")]
    [SerializeField] private float enemyFreezeRadius = 8f;

    private readonly HashSet<PlayerSafeZoneStatus> activePlayers = new HashSet<PlayerSafeZoneStatus>();

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerSafeZoneStatus(other, out PlayerSafeZoneStatus status))
            return;

        status.EnterSafeZone(this, enemyFreezeRadius);
        activePlayers.Add(status);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayerSafeZoneStatus(other, out PlayerSafeZoneStatus status))
            return;

        status.ExitSafeZone(this);
        if (!status.IsSafeZoneActive)
            activePlayers.Remove(status);
    }

    private void OnDisable()
    {
        foreach (PlayerSafeZoneStatus status in activePlayers)
        {
            if (status != null)
                status.ClearSafeZone(this);
        }

        activePlayers.Clear();
    }

    private bool TryGetPlayerSafeZoneStatus(Collider2D other, out PlayerSafeZoneStatus status)
    {
        status = null;
        if (other == null || !IsInLayerMask(other.gameObject, playerLayers))
            return false;

        Transform root = ResolvePlayerRoot(other);
        if (root == null)
            return false;

        if (!string.IsNullOrWhiteSpace(playerTag) && !root.CompareTag(playerTag))
            return false;

        if (!root.TryGetComponent(out status))
            status = root.gameObject.AddComponent<PlayerSafeZoneStatus>();

        return status != null;
    }

    private static Transform ResolvePlayerRoot(Collider2D other)
    {
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
