using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Prefabs to spawn (exactly two)")]
    public GameObject prefabA;
    public GameObject prefabB;

    [Header("Timing")]
    public float spawnInterval = 2f;

    [Header("Where to spawn")]
    public Transform[] spawnPoints; // optional; if empty, uses this.transform

    [Header("Control")]
    public bool autoStart = true;

    float _timer;
    bool _spawning;

    void Start()
    {
        _spawning = autoStart;
        _timer = 0f;

        if (!prefabA || !prefabB)
            Debug.LogWarning("[Spawner] Assign both prefabA and prefabB in the Inspector.", this);
    }

    void Update()
    {
        if (!_spawning || (!prefabA && !prefabB)) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnOne();
        }
    }

    void SpawnOne()
    {
        // Pick prefab randomly (50/50)
        GameObject pick = Random.value < 0.5f ? prefabA : prefabB;
        if (!pick) pick = prefabA ? prefabA : prefabB; // fallback if one is missing
        if (!pick) return;

        // Pick spawn point (or self)
        Transform point = transform;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int i = Random.Range(0, spawnPoints.Length);
            if (spawnPoints[i]) point = spawnPoints[i];
        }

        Instantiate(pick, point.position, point.rotation);
    }

    // Public controls
    public void StartSpawning()
    {
        _spawning = true;
        _timer = 0f;
    }

    public void StopSpawning()
    {
        _spawning = false;
    }

    public void SetInterval(float seconds)
    {
        spawnInterval = Mathf.Max(0f, seconds);
    }
}
