using UnityEngine;

[System.Serializable]
public class UpgradeRanges
{
    [Header("Shared")]
    public Vector2 damageMult = new Vector2(1.10f, 1.30f);
    public Vector2Int damageFlatAdd = new Vector2Int(3, 12);
    public Vector2 atkSpeedFrac = new Vector2(0.10f, 0.25f);
    public Vector2 critChanceAdd = new Vector2(0.05f, 0.20f);
    public Vector2 critMultAdd = new Vector2(0.25f, 1.00f);

    [Header("Health / Defense")]
    public Vector2Int hpFlatAdd = new Vector2Int(15, 60);
    public Vector2 hpMult = new Vector2(1.05f, 1.25f);
    public Vector2 regenAdd = new Vector2(0.10f, 1.50f);
    public Vector2 armorAdd = new Vector2(1.0f, 6.0f);
    public Vector2 evasionAdd = new Vector2(2.0f, 12.0f);
    public Vector2 armorMult = new Vector2(1.05f, 1.25f);
    public Vector2 evasionMult = new Vector2(1.05f, 1.25f);
    public Vector2 resistAdd = new Vector2(0.05f, 0.20f);


    [Header("Knife-only")]
    public Vector2 knifeRadiusMult = new Vector2(1.10f, 1.30f);
    public Vector2 knifeSplashRadiusMult = new Vector2(1.10f, 1.30f);
    public Vector2 knifeOnslaughtOnKillChance = new Vector2(0.15f, 0.35f);
    // lifesteal and multi-target removed

    [Header("Shooter-only")]
    public Vector2 shooterLifetimeAdd = new Vector2(0.5f, 2.0f);
    public Vector2 shooterForceAdd = new Vector2(1.0f, 4.0f);
    public Vector2 shooterSpreadReduceFrac = new Vector2(0.10f, 0.35f);
}
