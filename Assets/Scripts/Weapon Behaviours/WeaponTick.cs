// WeaponTick.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WeaponTick : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds between tick cycles (or between burst starts).")]
    [SerializeField] public float interval = 1f;
    [Tooltip("If true, starts ticking automatically on Awake.")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("If true, the tick repeats. If false, fires only once.")]
    [SerializeField] private bool repeat = true;
    [Tooltip("If true, uses unscaled time (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Burst")]
    [Tooltip("Enable to fire multiple ticks per cycle.")]
    [SerializeField] private bool burstEnabled = false;
    [Tooltip("How many ticks to fire per burst (>= 1).")]
    [SerializeField] public int burstCount = 3;
    [Tooltip("Seconds between ticks inside the burst.")]
    [SerializeField] public float burstSpacing = 0.1f;

    [Header("Event")]
    public UnityEvent onTick;

    private Coroutine tickCoroutine;
    private PlayerSafeZoneStatus safeZoneStatus;

    private void Awake()
    {
        if (startOnAwake)
            StartTick();
    }

    /// <summary>Begins the tick timer. If already running, restarts it.</summary>
    public void StartTick()
    {
        StopTick();
        tickCoroutine = StartCoroutine(TickRoutine());
    }

    /// <summary>Stops the timer if it's running.</summary>
    public void StopTick()
    {
        if (tickCoroutine != null)
        {
            StopCoroutine(tickCoroutine);
            tickCoroutine = null;
        }
    }

    /// <summary>Immediately invokes a single tick (does not affect coroutine schedule).</summary>
    public void TriggerNow()
    {
        InvokeTickIfAllowed();
    }

    /// <summary>Stops and restarts the timer from scratch.</summary>
    public void ResetAndStart()
    {
        StartTick();
    }

    /// <summary>Immediately fires a full burst now (respects burst settings).</summary>
    public void TriggerBurstNow()
    {
        if (!burstEnabled || burstCount <= 1)
        {
            InvokeTickIfAllowed();
            return;
        }
        StartCoroutine(DoBurstOnce());
    }

    private IEnumerator TickRoutine()
    {
        while (true)
        {
            // Read the current Inspector values every cycle so Play Mode changes
            // take effect without restarting the coroutine.
            float currentInterval = Mathf.Max(0f, interval);

            // Wait until the next cycle/burst start
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(currentInterval);
            else
                yield return new WaitForSeconds(currentInterval);

            int currentBurstCount = Mathf.Max(1, burstCount);
            if (burstEnabled && currentBurstCount > 1)
            {
                // Fire a burst
                for (int i = 0; i < currentBurstCount; i++)
                {
                    InvokeTickIfAllowed();

                    // Spacing between ticks inside the burst (skip after the last one)
                    if (i < currentBurstCount - 1)
                    {
                        float currentBurstSpacing = Mathf.Max(0f, burstSpacing);
                        if (useUnscaledTime)
                            yield return new WaitForSecondsRealtime(currentBurstSpacing);
                        else
                            yield return new WaitForSeconds(currentBurstSpacing);
                    }
                }
            }
            else
            {
                // Single tick mode
                InvokeTickIfAllowed();
            }

            if (!repeat) break;
        }

        tickCoroutine = null;
    }

    private IEnumerator DoBurstOnce()
    {
        float safeBurstSpacing = Mathf.Max(0f, burstSpacing);
        int safeBurstCount = Mathf.Max(1, burstCount);

        if (!burstEnabled || safeBurstCount <= 1)
        {
            InvokeTickIfAllowed();
            yield break;
        }

        for (int i = 0; i < safeBurstCount; i++)
        {
            InvokeTickIfAllowed();
            if (i < safeBurstCount - 1)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(safeBurstSpacing);
                else
                    yield return new WaitForSeconds(safeBurstSpacing);
            }
        }
    }

    private void InvokeTickIfAllowed()
    {
        if (IsBlockedBySafeZone())
            return;

        onTick?.Invoke();
    }

    private bool IsBlockedBySafeZone()
    {
        if (safeZoneStatus == null)
            safeZoneStatus = GetComponentInParent<PlayerSafeZoneStatus>();

        return safeZoneStatus != null && safeZoneStatus.IsSafeZoneActive;
    }

    private void OnDisable()
    {
        StopTick();
    }
}
