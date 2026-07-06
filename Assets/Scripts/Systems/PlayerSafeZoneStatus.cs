using System.Collections.Generic;
using UnityEngine;

public class PlayerSafeZoneStatus : MonoBehaviour
{
    private readonly Dictionary<Object, SafeZoneEntry> activeSources = new Dictionary<Object, SafeZoneEntry>();

    public bool IsSafeZoneActive => activeSources.Count > 0;
    public float EnemyFreezeRadius { get; private set; }

    public void EnterSafeZone(Object source, float enemyFreezeRadius)
    {
        if (source == null) return;

        if (activeSources.TryGetValue(source, out SafeZoneEntry entry))
        {
            entry.referenceCount++;
            entry.enemyFreezeRadius = Mathf.Max(entry.enemyFreezeRadius, enemyFreezeRadius);
            activeSources[source] = entry;
        }
        else
        {
            activeSources.Add(source, new SafeZoneEntry
            {
                referenceCount = 1,
                enemyFreezeRadius = Mathf.Max(0f, enemyFreezeRadius)
            });
        }

        RecalculateEnemyFreezeRadius();
    }

    public void ExitSafeZone(Object source)
    {
        if (source == null) return;
        if (!activeSources.TryGetValue(source, out SafeZoneEntry entry)) return;

        entry.referenceCount--;
        if (entry.referenceCount <= 0)
            activeSources.Remove(source);
        else
            activeSources[source] = entry;

        RecalculateEnemyFreezeRadius();
    }

    public void ClearSafeZone(Object source)
    {
        if (source == null) return;

        if (activeSources.Remove(source))
            RecalculateEnemyFreezeRadius();
    }

    private void RecalculateEnemyFreezeRadius()
    {
        float maxRadius = 0f;
        foreach (SafeZoneEntry entry in activeSources.Values)
            maxRadius = Mathf.Max(maxRadius, entry.enemyFreezeRadius);

        EnemyFreezeRadius = maxRadius;
    }

    private struct SafeZoneEntry
    {
        public int referenceCount;
        public float enemyFreezeRadius;
    }
}
