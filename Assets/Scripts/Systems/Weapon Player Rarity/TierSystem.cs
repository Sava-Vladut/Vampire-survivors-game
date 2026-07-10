using UnityEngine;

[System.Serializable]
public class TierSystem
{
    [Header("Tier (1 = strongest, 5 = weakest)")]
    [Range(1, 5)] public int damagePercent = 5;
    [Range(1, 5)] public int damageFlat = 5;
    [Range(1, 5)] public int attackSpeed = 5;
    [Range(1, 5)] public int critChance = 5;
    [Range(1, 5)] public int critMultiplier = 5;

    [Header("Mana (eligible weapons only)")]
    [Range(1, 5)] public int manaCostFlat = 5;
    [Range(1, 5)] public int manaCostPercent = 5;
    [Range(1, 5)] public int manaMax = 5;
    [Range(1, 5)] public int manaRegen = 5;

    [Header("Health / Defense (1=best, 5=worst)")]
    [Range(1, 5)] public int hpFlat = 5;
    [Range(1, 5)] public int hpPercent = 5;
    [Range(1, 5)] public int regen = 5;
    [Range(1, 5)] public int armor = 5;
    [Range(1, 5)] public int evasion = 5;
    [Range(1, 5)] public int armorPercent = 5;
    [Range(1, 5)] public int evasionPercent = 5;
    [Range(1, 5)] public int resist = 5;


    [Header("Knife")]
    [Range(1, 5)] public int knifeRadius = 5;
    [Range(1, 5)] public int knifeSplashRadius = 5;
    [Range(1, 5)] public int knifeOnslaughtOnKill = 5;

    [Header("Shooter")]
    [Range(1, 5)] public int shooterLifetime = 5;
    [Range(1, 5)] public int shooterForce = 5;
    [Range(1, 5)] public int shooterAccuracy = 5;

    [Header("Curve (optional)")]
    public AnimationCurve multiplierCurve; // X: 0=Tier5..1=Tier1, Y: mult
    public bool useCurve = false;
    public Vector2 defaultMinMax = new Vector2(0.5f, 2.0f);

    public void RollAll(System.Random rng)
    {
        damagePercent = Roll(rng);
        damageFlat = Roll(rng);
        attackSpeed = Roll(rng);
        critChance = Roll(rng);
        critMultiplier = Roll(rng);
        manaCostFlat = Roll(rng);
        manaCostPercent = Roll(rng);
        manaMax = Roll(rng);
        manaRegen = Roll(rng);

        hpFlat = Roll(rng);
        hpPercent = Roll(rng);
        regen = Roll(rng);
        armor = Roll(rng);
        evasion = Roll(rng);
        armorPercent = Roll(rng);
        evasionPercent = Roll(rng);
        resist = Roll(rng);

        knifeRadius = Roll(rng);
        knifeSplashRadius = Roll(rng);
        knifeOnslaughtOnKill = Roll(rng);

        shooterLifetime = Roll(rng);
        shooterForce = Roll(rng);
        shooterAccuracy = Roll(rng);
    }

    public float Mult(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 5);
        if (useCurve && multiplierCurve != null && multiplierCurve.length > 0)
        {
            // Enforce monotonicity across tiers: Tier 1 >= Tier 2 >= ... >= Tier 5
            // Sample all tiers from the curve, then fix any inversions.
            // x maps 5->0 .. 1->1
            float[] raw = new float[6]; // 1..5
            for (int ti = 1; ti <= 5; ti++)
            {
                float x = (5 - ti) / 4f;
                raw[ti] = Mathf.Max(0f, multiplierCurve.Evaluate(x));
            }
            // Make non-increasing as tier number increases (1 best)
            for (int ti = 4; ti >= 1; ti--)
            {
                if (raw[ti] < raw[ti + 1]) raw[ti] = raw[ti + 1];
            }
            return raw[tier];
        }
        float t = (5 - tier) / 4f;
        return Mathf.Lerp(Mathf.Max(0f, defaultMinMax.x), Mathf.Max(0f, defaultMinMax.y), t);
    }

    public Vector2 Scale(Vector2 baseRange, int tier)
    {
        float m = Mult(tier);
        return new Vector2(baseRange.x * m, baseRange.y * m);
    }

    public Vector2Int Scale(Vector2Int baseRange, int tier, int minClamp = int.MinValue)
    {
        float m = Mult(tier);
        int x = Mathf.RoundToInt(baseRange.x * m);
        int y = Mathf.RoundToInt(baseRange.y * m);
        if (x > y) (x, y) = (y, x);
        x = Mathf.Max(minClamp, x); y = Mathf.Max(minClamp, y);
        return new Vector2Int(x, y);
    }

    public Vector2 ScaleMultiplierLike(Vector2 baseRange, int tier)
    {
        float m = Mult(tier);
        float a = 1f + (baseRange.x - 1f) * m;
        float b = 1f + (baseRange.y - 1f) * m;
        if (a > b) (a, b) = (b, a);
        return new Vector2(a, b);
    }

    static int Roll(System.Random rng) => rng.Next(1, 6);
}
