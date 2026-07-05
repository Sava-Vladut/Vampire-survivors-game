using UnityEngine;

public class PrefabInstantiator : MonoBehaviour
{
    [Tooltip("Prefab to instantiate.")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Delay in seconds between each instantiation.")]
    [SerializeField] private float delay = 1f;

    [Tooltip("Instantiate under this transform instead of world space. Defaults to this object's transform.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("If true, stop after spawning maxCount instances.")]
    [SerializeField] private bool useCountLimit;
    [SerializeField] private int maxCount = 10;

    [Tooltip("If true, stop spawning after maxTime seconds have passed.")]
    [SerializeField] private bool useTimeLimit;
    [SerializeField] private float maxTime = 10f;

    private float timer;
    private float elapsed;
    private int spawnedCount;
    private bool finished;

    private void Reset()
    {
        spawnPoint = transform;
    }

    private void Update()
    {
        if (finished) return;

        elapsed += Time.deltaTime;
        if (useTimeLimit && elapsed >= maxTime)
        {
            finished = true;
            return;
        }

        timer += Time.deltaTime;
        if (timer >= delay)
        {
            timer -= delay;
            Spawn();

            if (useCountLimit && spawnedCount >= maxCount)
            {
                finished = true;
            }
        }
    }

    private void Spawn()
    {
        if (prefab == null) return;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        Instantiate(prefab, point.position, point.rotation);
        spawnedCount++;
    }
}
