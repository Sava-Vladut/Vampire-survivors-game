using System;
using UnityEngine;

/// <summary>Extension point for adding new transient offer families.</summary>
public interface IPowerUpOfferSource
{
    void GenerateOffers(PowerUpOfferGenerationContext context);
}

public sealed class PowerUpOfferGenerationContext
{
    private readonly Func<PowerUp, GameObject, bool> registerOffer;

    public PowerUpOfferGenerationContext(
        PowerUpChooser chooser,
        Transform player,
        int offersPerWeapon,
        int offersPerAccessory,
        float firstOnHitBaseChance,
        Func<PowerUp, GameObject, bool> registerOffer)
    {
        Chooser = chooser;
        Player = player;
        OffersPerWeapon = Mathf.Max(0, offersPerWeapon);
        OffersPerAccessory = Mathf.Max(0, offersPerAccessory);
        FirstOnHitBaseChance = Mathf.Clamp01(firstOnHitBaseChance);
        this.registerOffer = registerOffer;
    }

    public PowerUpChooser Chooser { get; }
    public Transform Player { get; }
    public int OffersPerWeapon { get; }
    public int OffersPerAccessory { get; }
    public float FirstOnHitBaseChance { get; }

    public GameObject CreateOfferObject(Transform target, string objectName)
    {
        if (target == null) return null;
        var offerObject = new GameObject(objectName);
        offerObject.SetActive(false);
        offerObject.transform.SetParent(target, false);
        return offerObject;
    }

    public bool RegisterOffer(PowerUp offer, GameObject offerObject)
    {
        return registerOffer != null && registerOffer(offer, offerObject);
    }
}

/// <summary>Optional per-item override for runtime-generated upgrade drops.</summary>
[DisallowMultipleComponent]
public class GeneratedUpgradeEligibility : MonoBehaviour
{
    public bool allowGeneratedUpgrades = true;
}
