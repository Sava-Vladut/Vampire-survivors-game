using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates transient offer sources and owns their cleanup. New systems can
/// implement IPowerUpOfferSource on this GameObject or register a source at runtime.
/// </summary>
[DisallowMultipleComponent]
public class RandomUpgradeGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Chooser whose available pool receives generated offers. Auto-found if empty.")]
    [SerializeField] private PowerUpChooser powerUpChooser;

    [Header("Offers")]
    [Min(1)][SerializeField] private int offersPerWeapon = 3;
    [Min(1)][SerializeField] private int offersPerAccessory = 2;

    private readonly List<TrackedOffer> activeOffers = new();
    private readonly List<IPowerUpOfferSource> registeredSources = new();
    private readonly List<IPowerUpOfferSource> sources = new();

    private void Awake() => ResolveChooser();

    private void OnDisable() => ClearOffers();

    public void RegisterSource(IPowerUpOfferSource source)
    {
        if (source != null && !registeredSources.Contains(source))
            registeredSources.Add(source);
    }

    public void UnregisterSource(IPowerUpOfferSource source)
    {
        if (source != null)
            registeredSources.Remove(source);
    }

    public void RefreshOffers() => RefreshOffers(0.15f);

    public void RefreshOffers(float firstOnHitBaseChance)
    {
        ClearOffers();
        ResolveChooser();

        if (powerUpChooser == null)
        {
            Debug.LogWarning("[RandomUpgradeGenerator] No PowerUpChooser is available.", this);
            return;
        }

        Transform player = null;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        BuildSourceList();
        var context = new PowerUpOfferGenerationContext(
            powerUpChooser,
            player,
            offersPerWeapon,
            offersPerAccessory,
            firstOnHitBaseChance,
            RegisterOffer);

        for (int i = 0; i < sources.Count; i++)
        {
            try
            {
                sources[i].GenerateOffers(context);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    public void ClearOffers()
    {
        for (int i = 0; i < activeOffers.Count; i++)
        {
            TrackedOffer tracked = activeOffers[i];
            bool wasSelected = powerUpChooser != null &&
                               powerUpChooser.selectedPowerUps != null &&
                               powerUpChooser.selectedPowerUps.Contains(tracked.Offer);

            if (wasSelected) continue;

            powerUpChooser?.RemoveAvailable(tracked.Offer);
            if (tracked.Object != null)
                Destroy(tracked.Object);
        }

        activeOffers.Clear();
    }

    private void BuildSourceList()
    {
        sources.Clear();
        sources.Add(new WeaponUpgradeOfferSource());
        sources.Add(new AccessoryUpgradeOfferSource());
        sources.Add(new FirstOnHitStatusOfferSource());

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPowerUpOfferSource source && !sources.Contains(source))
                sources.Add(source);
        }

        for (int i = 0; i < registeredSources.Count; i++)
        {
            IPowerUpOfferSource source = registeredSources[i];
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }
    }

    private bool RegisterOffer(PowerUp offer, GameObject offerObject)
    {
        if (offer == null || offerObject == null || powerUpChooser == null)
        {
            if (offerObject != null) Destroy(offerObject);
            return false;
        }

        if (!powerUpChooser.TryAddAvailable(offer, allowDuplicateIdentity: true))
        {
            Destroy(offerObject);
            return false;
        }

        activeOffers.Add(new TrackedOffer(offer, offerObject));
        return true;
    }

    private void ResolveChooser()
    {
        if (powerUpChooser == null)
            powerUpChooser = GetComponent<PowerUpChooser>();
        if (powerUpChooser == null)
            powerUpChooser = FindAnyObjectByType<PowerUpChooser>();
    }

    private readonly struct TrackedOffer
    {
        public TrackedOffer(PowerUp offer, GameObject offerObject)
        {
            Offer = offer;
            Object = offerObject;
        }

        public PowerUp Offer { get; }
        public GameObject Object { get; }
    }
}
