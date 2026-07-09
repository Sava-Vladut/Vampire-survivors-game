using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpRandomMerger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PowerUpChooser powerUpChooser;

    [Header("Source Object Handling")]
    [SerializeField] private SourceObjectHandling sourceObjectHandling = SourceObjectHandling.Disable;

    public enum SourceObjectHandling { None, Disable, Destroy }

    // Right-click component (gear icon) -> "Merge/Two Random Accessories"
    [ContextMenu("Merge/Two Random Accessories")]
    private void MergeTwoRandomAccessories()
    {
        MergeTwoRandomOfType(isAccessory: true);
    }

    // Right-click component (gear icon) -> "Merge/Two Random Weapons"
    [ContextMenu("Merge/Two Random Weapons")]
    private void MergeTwoRandomWeapons()
    {
        MergeTwoRandomOfType(isAccessory: false);
    }

    private void MergeTwoRandomOfType(bool isAccessory)
    {
        if (powerUpChooser == null || powerUpChooser.selectedPowerUps == null)
        {
            Debug.LogWarning("[PowerUpRandomMerger] Missing PowerUpChooser or selected list.");
            return;
        }

        // Gather eligible items (already selected)
        List<PowerUp> pool = new List<PowerUp>();
        foreach (var pu in powerUpChooser.selectedPowerUps)
        {
            if (pu == null || pu.IsUpgrade) continue;
            if (isAccessory && pu.IsAccessory) pool.Add(pu);
            else if (!isAccessory && pu.IsWeapon) pool.Add(pu);
        }

        if (pool.Count < 2)
        {
            Debug.Log($"[PowerUpRandomMerger] Not enough {(isAccessory ? "accessories" : "weapons")} to merge (need 2).");
            return;
        }

        // Pick two distinct random indices
        int iA = Random.Range(0, pool.Count);
        int iB = iA;
        while (iB == iA) iB = Random.Range(0, pool.Count);

        var a = pool[iA];
        var b = pool[iB];

        bool disable = sourceObjectHandling == SourceObjectHandling.Disable;
        bool destroy = sourceObjectHandling == SourceObjectHandling.Destroy;
        powerUpChooser.TryRemoveSelected(a, disable, destroy);
        powerUpChooser.TryRemoveSelected(b, disable, destroy);

        Debug.Log($"[PowerUpRandomMerger] Merged and removed: \"{a?.powerUpName}\" + \"{b?.powerUpName}\".");
    }

}
