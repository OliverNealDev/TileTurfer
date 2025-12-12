using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private GameObject enemyPrefab;

    [Header("Difficulty Scaling")]
    [Tooltip("Enemies allowed when you have 0% Turf")]
    [SerializeField] private int minEnemies = 3;
    [Tooltip("Enemies allowed when you have 100% Turf")]
    [SerializeField] private int maxEnemies = 40; 

    [Tooltip("Time between spawns at 0% Turf (Slow)")]
    [SerializeField] private float slowSpawnRate = 4.0f;
    [Tooltip("Time between spawns at 100% Turf (Fast)")]
    [SerializeField] private float fastSpawnRate = 0.5f;

    private float timeSinceLastSpawn = 0f;
    private Transform playerTransform;

    void Start()
    {
        if (turfManager == null) turfManager = FindFirstObjectByType<TurfManager>();
        
        // Find Player for safety checks
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        // Spawn a few to start
        for(int i = 0; i < minEnemies; i++)
        {
            SpawnEnemy();
        }
    }
    
    void Update()
    {
        HandleSpawning();
    }
    
    void HandleSpawning()
    {
        float progress = 0f;
        if (turfManager != null) progress = turfManager.GetTurfPercentage();

        int currentCap = Mathf.RoundToInt(Mathf.Lerp(minEnemies, maxEnemies, progress));
        float currentRate = Mathf.Lerp(slowSpawnRate, fastSpawnRate, progress);

        int activeEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;

        if (activeEnemies < currentCap)
        {
            timeSinceLastSpawn += Time.deltaTime;
            
            if (timeSinceLastSpawn >= currentRate)
            {
                SpawnEnemy();
                timeSinceLastSpawn = 0f;
            }
        }
    }
    
    void SpawnEnemy()
    {
        if (enemyPrefab == null || turfManager == null) return;

        Vector3 pPos = Vector3.zero;
        if (playerTransform != null) pPos = playerTransform.position;

        // Ask TurfManager for a valid spot, passing player pos for safety
        Vector3? spawnPos = turfManager.GetRandomEnemySpawnPoint(pPos, 8f);

        if (spawnPos.HasValue)
        {
            Instantiate(enemyPrefab, spawnPos.Value, Quaternion.identity);
        }
    }
}