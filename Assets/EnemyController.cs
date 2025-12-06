using System;
using System.Collections;
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
    
    [Header("Variation Settings")]
    [SerializeField] private float minSizeMult = 0.85f;
    [SerializeField] private float maxSizeMult = 1.15f;
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float colorVariance = 0.1f;
    
    private Vector3 targetScale;
    private Color specificEnemyColor;
    
    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip shootSound;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    private AudioSource audioSource;

    [Header("Combat")]
    [SerializeField] private GameObject bombPrefab; 
    [SerializeField] private float minBombInterval = 10f;
    [SerializeField] private float maxBombInterval = 120f;
    private float bombTimer;
    private float currentBombInterval;

    [Header("Enemy Shooting")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float fireRate = 4f; 
    [SerializeField] private float bulletSpeed = 6f;
    private float fireInterval;
    private float fireTimer;

    private Vector2 randomNearbyPoint;
    
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
        fireInterval = 1f / fireRate;
    }

    void Start()
    {
        if (!agent.isOnNavMesh) Debug.LogError("Enemy is NOT on NavMesh.");

        // Variation Logic
        float sizeMultiplier = UnityEngine.Random.Range(minSizeMult, maxSizeMult);
        targetScale = new Vector3(sizeMultiplier, sizeMultiplier, 1f);
        agent.speed = baseSpeed / sizeMultiplier;

        float rOffset = UnityEngine.Random.Range(-colorVariance, colorVariance);
        float gOffset = UnityEngine.Random.Range(-colorVariance, colorVariance);
        float bOffset = UnityEngine.Random.Range(-colorVariance, colorVariance);

        specificEnemyColor = new Color(
            Mathf.Clamp01(enemyColor.r + rOffset),
            Mathf.Clamp01(enemyColor.g + gOffset),
            Mathf.Clamp01(enemyColor.b + bOffset),
            enemyColor.a
        );

        Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        randomNearbyPoint = (Vector2)transform.position + randomDirection;

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
        if (transform.localScale.x < targetScale.x)
        {
            if (!isDisabled)
            {
                isDisabled = true;
                GetComponent<SpriteRenderer>().color = disabledColor;
            }
            if (agent.hasPath) agent.ResetPath(); 
            transform.localScale += Vector3.one * Time.deltaTime * 0.25f; 
            if (transform.localScale.x >= targetScale.x) transform.localScale = targetScale;
            return; 
        }

        // --- Active Phase ---
        if (isDisabled)
        {
            isDisabled = false;
            GetComponent<SpriteRenderer>().color = specificEnemyColor;
        }

        HandleBombSpawning();
            
        float distanceToPlayer = Vector2.Distance(transform.position, playerObj.transform.position);

        if (distanceToPlayer <= 5f)
        {
            agent.SetDestination(playerObj.transform.position);
            HandleShooting(); 
        }
        else
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                SetNewRandomDestination();
            }
        }
    }

    void HandleShooting()
    {
        fireTimer += Time.deltaTime;

        if (fireTimer >= fireInterval)
        {
            ShootAtPlayer();
            fireTimer = 0f;
        }
    }

    void ShootAtPlayer()
    {
        if (projectilePrefab == null || playerObj == null) return;

        Vector3 direction = (playerObj.transform.position - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        GameObject bullet = Instantiate(projectilePrefab, transform.position, rotation);

        bulletController bc = bullet.GetComponent<bulletController>();
        if (bc != null)
        {
            bc.Initialise(false, bulletSpeed, 2f, 1f, 0f);
        }

        if (audioSource != null && shootSound != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(shootSound, sfxVolume * 0.5f); 
        }
    }

    void HandleBombSpawning()
    {
        if (bombPrefab == null) return;
        bombTimer += Time.deltaTime;
        if (bombTimer >= currentBombInterval)
        {
            SpawnBomb();
            bombTimer = 0f;
            currentBombInterval = UnityEngine.Random.Range(minBombInterval, maxBombInterval);
        }
    }
    void SpawnBomb() { Instantiate(bombPrefab, transform.position, Quaternion.identity); }
    void SetNewRandomDestination()
    {
        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        Vector3 targetPos = transform.position + new Vector3(randomDir.x, randomDir.y, 0);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 2.0f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
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

            // If too small, JUST DIE (No animation)
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