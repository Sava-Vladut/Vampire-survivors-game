using UnityEngine;
using UnityEngine.Events;


[RequireComponent(typeof(Collider2D))]
public class Trigger2DEvent : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only objects on these layers can trigger the event.")]
    [SerializeField] private LayerMask triggerLayers = ~0; // default: all layers

    public bool destroyOnTrigger = true;

    [Header("Events")]
    public UnityEvent onTriggerEnter;
    public UnityEvent onTriggerExit;

    private void Reset()
    {
        // Ensure collider is a trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    public void PlaySoundAtPlayer(AudioClip sfx)
    {
        GameObject.FindGameObjectWithTag("Player").GetComponent<AudioSource>().PlayOneShot(sfx);
    }

    public void XPOrb(int value)
    {

        GameObject.FindGameObjectWithTag("GameController").GetComponent<XpSystem>().AddExperience(Random.Range(value, value * 2));
    }

    public void XpBoost()
    {
        var player = GameObject.FindGameObjectWithTag("Player").GetComponent<SimpleHealth>();
        if (player == null) return;
        player.GetComponent<StatusEffectSystem>().ApplyStatusEffect_Int(9, 30);
    }

    public void Hearth(float value)
    {
        var player = GameObject.FindGameObjectWithTag("Player").GetComponent<SimpleHealth>();
        if (player == null) return;

        int healAmount = Mathf.CeilToInt(player.maxHealth * value); // 10% of max health
        player.Heal(healAmount);
    }

    public void Speed()
    {
        var player = GameObject.FindGameObjectWithTag("Player").GetComponent<SimpleHealth>();
        if (player == null) return;
        player.GetComponent<StatusEffectSystem>().ApplyStatusEffect_Int(2, 10);
    }


    public void Magnet(float value)
    {
        FollowNearestOptimized[] fnp = FindObjectsByType<FollowNearestOptimized>(FindObjectsSortMode.None);

        foreach (var item in fnp)
        {
            if (item.transform.name.Contains("Xp"))
            {
                item.searchRadius = value;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInLayerMask(other.gameObject, triggerLayers)) return;
        onTriggerEnter?.Invoke();
        if (destroyOnTrigger)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsInLayerMask(other.gameObject, triggerLayers)) return;
        onTriggerExit?.Invoke();
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        return (mask.value & (1 << go.layer)) != 0;
    }
}
