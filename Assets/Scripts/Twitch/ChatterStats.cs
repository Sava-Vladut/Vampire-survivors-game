using TMPro;
using UnityEngine;

public class ChatterStats : MonoBehaviour
{
    [Header("UI (optional)")]
    public TextMeshProUGUI nameGUI;

    [Header("Chatter Power")]
    [Tooltip("Higher power makes the monster's rolled stats and rarity stronger.")]
    public int power = 0;

    [Header("Scaling Controls")]
    [Tooltip("How much we bias rarity towards higher tiers per power point. 0.02 = +2% weight scaling step.")]
    [Min(0f)] public float rarityBiasPerPower = 0.02f;

    [Tooltip("Multiplies stat roll ranges per power point. 0.015 = +1.5% per power.")]
    [Min(0f)] public float statRangeMultPerPower = 0.015f;

    [Tooltip("Extra scaling applied specifically to global attack cadence (WeaponTick).")]
    [Min(0f)] public float cadenceBoostPerPower = 0.01f;

    [Tooltip("Hard cap for the total multiplier applied to ranges (1 = no growth).")]
    [Min(1f)] public float maxRangeMultiplier = 3.0f;

    [Tooltip("Hard cap for rarity bias factor (1 = no bias).")]
    [Min(1f)] public float maxRarityBiasFactor = 4.0f;

    private void Start()
    {
        var mr = GetComponent<MonsterRarity>();
        if (mr != null)
        {
            ApplyPowerScaling(mr);
            ChatterBoss boss = GetComponent<ChatterBoss>();
            if (boss != null)
            {
                mr.ForceRarity(MonsterRarity.Rarity.Legendary);
                boss.MarkLegendaryApplied();
            }
            else
            {
                mr.RerollRarity(); // Reroll after scaling has been applied.
            }
        }
    }

    private void ApplyPowerScaling(MonsterRarity mr)
    {
        int p = Mathf.Max(0, power);

        // ===== 1) Rarity bias (favor higher rarities as power grows) =====
        float bias = 1f + p * rarityBiasPerPower;
        bias = Mathf.Min(bias, maxRarityBiasFactor);

        // Shift distribution: reduce Common, increase Uncommon/Rare/Legendary
        float comMul = 1f / bias;
        float uncMul = Mathf.Lerp(1f, bias, 0.33f);
        float rareMul = Mathf.Lerp(1f, bias, 0.66f);
        float legMul = bias;

        mr.weightCommon *= comMul;
        mr.weightUncommon *= uncMul;
        mr.weightRare *= rareMul;
        mr.weightLegendary *= legMul;

        // ===== 2) Range scaling (make all roll ranges stronger with power) =====
        float rangeMul = 1f + p * statRangeMultPerPower;
        rangeMul = Mathf.Clamp(rangeMul, 1f, maxRangeMultiplier);

        // Helper local funcs
        static Vector2Int ScaleV2I(Vector2Int v, float mul)
        {
            int a = Mathf.RoundToInt(v.x * mul);
            int b = Mathf.RoundToInt(v.y * mul);
            if (a > b) (a, b) = (b, a);
            return new Vector2Int(Mathf.Max(0, a), Mathf.Max(0, b));
        }

        static Vector2 ScaleV2(Vector2 v, float mul)
        {
            float a = v.x * mul;
            float b = v.y * mul;
            if (a > b) (a, b) = (b, a);
            return new Vector2(a, b);
        }

        // --- Enemy rolls (we scale ranges only; MonsterRarity will apply them)
        mr.hpFlatAdd = ScaleV2I(mr.hpFlatAdd, rangeMul);
        mr.hpMult = ScaleV2(mr.hpMult, rangeMul);
        mr.regenAdd = ScaleV2(mr.regenAdd, rangeMul);
        mr.armorAdd = ScaleV2(mr.armorAdd, rangeMul);

        // --- Global cadence (give it extra oomph if desired)
        float cadenceMul = Mathf.Clamp(rangeMul * (1f + p * cadenceBoostPerPower), 1f, maxRangeMultiplier);
        mr.atkSpeedFracAll = ScaleV2(mr.atkSpeedFracAll, cadenceMul);

        // --- Knife
        mr.KnifeDamageFlat = ScaleV2I(mr.KnifeDamageFlat, rangeMul);
        mr.KnifeDamageMult = ScaleV2(mr.KnifeDamageMult, rangeMul);
        mr.KnifeLifestealAdd = ScaleV2(mr.KnifeLifestealAdd, rangeMul);
        mr.KnifeCritChanceAdd = ScaleV2(mr.KnifeCritChanceAdd, rangeMul);
        mr.KnifeCritMultAdd = ScaleV2(mr.KnifeCritMultAdd, rangeMul);

        // --- Shooter
        mr.shooterDamageFlat = ScaleV2I(mr.shooterDamageFlat, rangeMul);
        mr.shooterDamageMult = ScaleV2(mr.shooterDamageMult, rangeMul);
        mr.shooterProjectilesAdd = ScaleV2I(mr.shooterProjectilesAdd, rangeMul);

        // --- Loot
        LootTable2D lt2d = GetComponent<LootTable2D>();
        if (lt2d != null)
        {
            foreach (LootTable2D.AmountOption item in lt2d.amountOptions)
            {
                int bonus = Mathf.Min(Mathf.RoundToInt(power / 20f), 3); // cap at +3
                item.amount += bonus;
            }
        }
    }
}
