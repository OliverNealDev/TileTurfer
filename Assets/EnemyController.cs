using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AudioSource))] 
public class EnemyController : MonoBehaviour
{ 
    private NavMeshAgent agent;
    [SerializeField] private GameObject playerObj;
    
    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    [SerializeField] private Color enemyColor;
    [SerializeField] private Color disabledColor;
    private bool isDisabled = false;
    private Vector3Int lastCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    private AudioSource audioSource;

    [Header("Combat (Bombs)")]
    [SerializeField] private GameObject bombPrefab; // Assign your Bomb Prefab here
    [SerializeField] private float minBombInterval = 10f;
    [SerializeField] private float maxBombInterval = 120f;
    private float bombTimer;
    private float currentBombInterval;

    private Vector2 randomNearbyPoint;
    private bool isChasing;
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>(); 

        agent.updateRotation = false;
        agent.updateUpAxis = false;

        var pos = transform.position;
        transform.position = new Vector3(pos.x, pos.y, 0f);
        
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (turfManager == null) turfManager = FindAnyObjectByType<TurfManager>();
        if (turfTilemap == null) turfTilemap = GameObject.Find("TurfTilemap")?.GetComponent<Tilemap>();
        
        transform.localScale = new Vector3(0.01f, 0.01f, 0.01f); 
    }

    void Start()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Enemy is NOT on NavMesh at Start. Check Z position and NavMesh baking.");
        }
        
        Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        randomNearbyPoint = (Vector2)transform.position + randomDirection;

        // Initialize the first bomb timer
        currentBombInterval = UnityEngine.Random.Range(minBombInterval, maxBombInterval);
    }

    void FixedUpdate()
    {
        PaintTurfUnderEnemy();
    }

    void Update()
    {
        if (agent == null || playerObj == null) return;
        
        // --- Growing Phase ---
        if (transform.localScale.x < 1f)
        {
            if (!isDisabled)
            {
                isDisabled = true;
                GetComponent<SpriteRenderer>().color = disabledColor;
            }
            
            if (agent.hasPath) agent.ResetPath(); 

            transform.localScale += Vector3.one * Time.deltaTime * 0.1f;
            if (transform.localScale.x >= 1f) transform.localScale = Vector3.one;
            
            return; 
        }

        // --- Active Phase ---
        if (isDisabled)
        {
            isDisabled = false;
            GetComponent<SpriteRenderer>().color = enemyColor;
        }

        // Handle Bomb Spawning (Only when active)
        HandleBombSpawning();
            
        float distanceToPlayer = Vector2.Distance(transform.position, playerObj.transform.position);

        // 1. Chase Player
        if (distanceToPlayer <= 5f)
        {
            agent.SetDestination(playerObj.transform.position);
        }
        // 2. Roam Randomly
        else
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                SetNewRandomDestination();
            }
        }
    }

    void HandleBombSpawning()
    {
        if (bombPrefab == null) return;

        bombTimer += Time.deltaTime;

        if (bombTimer >= currentBombInterval)
        {
            SpawnBomb();
            
            // Reset timer and pick a NEW random interval for the next drop
            bombTimer = 0f;
            currentBombInterval = UnityEngine.Random.Range(minBombInterval, maxBombInterval);
        }
    }

    void SpawnBomb()
    {
        // Instantiate the bomb at the enemy's current position
        Instantiate(bombPrefab, transform.position, Quaternion.identity);
    }

    void SetNewRandomDestination()
    {
        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        Vector3 targetPos = transform.position + new Vector3(randomDir.x, randomDir.y, 0);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void PaintTurfUnderEnemy()
    {
        if (turfTilemap == null || turfManager == null) return;

        Vector3Int cellPos = turfTilemap.WorldToCell(transform.position);
        
        if (cellPos == lastCell) return;

        turfManager.RegisterTile(cellPos, false);

        lastCell = cellPos;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("PlayerBullet"))
        {
            Destroy(other.gameObject);
            
            if (audioSource != null && hitSound != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(hitSound, sfxVolume);
            }

            transform.localScale -= Vector3.one * 0.2f;

            if (transform.localScale.x <= 0.25f)
            {
                if (deathSound != null)
                {
                    AudioSource.PlayClipAtPoint(deathSound, transform.position, sfxVolume);
                }
                
                Destroy(gameObject);
            }
        }
    }
}