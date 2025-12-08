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
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true; // TOGGLE THIS OFF to stop console spam

    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    [SerializeField] private Color enemyColor;
    private bool isGrowing = true; 
    
    [Header("Variation Settings")]
    [SerializeField] private float minSizeMult = 0.85f;
    [SerializeField] private float maxSizeMult = 1.15f;
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float colorVariance = 0.1f;
    
    [Tooltip("Time in seconds to reach full size")]
    [SerializeField] private float growthDuration = 0.5f; 
    private float growthTimer = 0f;
    
    private Vector3 targetScale;
    private Color specificEnemyColor;

    [Header("Health & Damage")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float healthRegenRate = 0.5f; 
    [SerializeField] private Color damageFadeColor = new Color(0.5f, 0f, 0.5f, 1f); 
    [SerializeField] private Color deathPaintColor = new Color(0.3f, 0f, 1f, 1f); 
    
    [Header("Inflation & Explosion")]
    [SerializeField] private float maxInflationMult = 1.5f; 
    [SerializeField] private float explosionDuration = 0.25f;
    [SerializeField] private float explosionPopScale = 2.5f; 
    [SerializeField] private int deathExplosionRadius = 2;
    
    private float currentHealth;
    private bool isDead = false;

    // --- AI SENSES ---
    [Header("AI & Senses")]
    [SerializeField] private float visionRange = 7f; 
    [SerializeField] private float chasePersistenceDuration = 3f;
    [Tooltip("Select 'Default' (Walls) and 'Player'. Do NOT select 'Enemy'.")]
    [SerializeField] private LayerMask obstacleMask; 
    private float lastSawPlayerTime = -10f; 

    // State tracking for logs to prevent spamming the same log every frame
    private enum AIState { Spawning, Roaming, ChasingMemory, ChasingVisible }
    private AIState currentState = AIState.Spawning;

    [Header("Visuals")]
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite angrySprite;
    [SerializeField] private float rotationSpeed = 5f;
    private SpriteRenderer spriteRenderer;
    private Collider2D enemyCollider;
    
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
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;

        var pos = transform.position;
        transform.position = new Vector3(pos.x, pos.y, 0f);
        
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (turfManager == null) turfManager = FindAnyObjectByType<TurfManager>();
        if (turfTilemap == null) turfTilemap = GameObject.Find("TurfTilemap")?.GetComponent<Tilemap>();
        
        transform.localScale = Vector3.zero; 
        fireInterval = 1f / fireRate;
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (!agent.isOnNavMesh) Debug.LogError($"[Enemy {gameObject.name}] NOT on NavMesh!");

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

        if (neutralSprite != null) spriteRenderer.sprite = neutralSprite;
        
        spriteRenderer.color = specificEnemyColor;
    }

    void FixedUpdate()
    {
        if (isDead) return;
        PaintTurfUnderEnemy();
    }

    void Update()
    {
        if (isDead) return; 
        if (agent == null || playerObj == null) return;
        
        if (isGrowing)
        {
            growthTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growthTimer / growthDuration);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            if (growthTimer >= growthDuration)
            {
                isGrowing = false;
                transform.localScale = targetScale;
                currentState = AIState.Roaming; // Ready to start AI
            }
        }
        else
        {
            if (currentHealth < maxHealth)
            {
                currentHealth += healthRegenRate * Time.deltaTime;
                if (currentHealth > maxHealth) currentHealth = maxHealth;
            }

            UpdateColorBasedOnHealth();
            UpdateInflationBasedOnHealth();
        }
        
        HandleBombSpawning();
        HandleAI();
    }

    void HandleAI()
    {
        if (isGrowing) return;

        bool canSeePlayer = CheckLineOfSight();

        if (canSeePlayer)
        {
            lastSawPlayerTime = Time.time;
            
            // Log state change
            if (currentState != AIState.ChasingVisible)
            {
                if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] ACQUIRED TARGET: Player Visible!");
                currentState = AIState.ChasingVisible;
            }
        }

        bool shouldChase = Time.time - lastSawPlayerTime < chasePersistenceDuration;

        if (shouldChase)
        {
            // Log state change (Memory)
            if (!canSeePlayer && currentState != AIState.ChasingMemory)
            {
                if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] LOST VISUAL: Chasing Memory...");
                currentState = AIState.ChasingMemory;
            }

            agent.SetDestination(playerObj.transform.position);
            
            if (angrySprite != null) spriteRenderer.sprite = angrySprite;

            Vector3 direction = (playerObj.transform.position - transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            // Only shoot if we can ACTUALLY see them
            if (canSeePlayer)
            {
                HandleShooting();
            }
        }
        else
        {
            // Log state change
            if (currentState != AIState.Roaming)
            {
                if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] GAVE UP: Roaming.");
                currentState = AIState.Roaming;
            }

            if (neutralSprite != null) spriteRenderer.sprite = neutralSprite;

            if (agent.velocity.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(agent.velocity.y, agent.velocity.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * (rotationSpeed * 0.5f));
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                SetNewRandomDestination();
            }
        }
    }

    bool CheckLineOfSight()
    {
        if (playerObj == null) return false;

        float distance = Vector2.Distance(transform.position, playerObj.transform.position);
        
        if (distance > visionRange) return false;

        Vector3 direction = (playerObj.transform.position - transform.position).normalized;
        
        // VISUAL DEBUG: Draw a Yellow line to show where the enemy is looking
        if (enableDebugLogs) Debug.DrawRay(transform.position, direction * visionRange, Color.yellow);

        // Perform Raycast
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, visionRange, obstacleMask);

        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Player"))
            {
                if (enableDebugLogs) Debug.DrawLine(transform.position, hit.point, Color.green);
                return true;
            }
            else
            {
                // DIAGNOSTIC LOG: This will tell you exactly what is blocking the view
                if (enableDebugLogs) 
                {
                    Debug.Log($"[Enemy Vision] Blocked by: '{hit.collider.name}' on Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                    Debug.DrawLine(transform.position, hit.point, Color.red);
                }
            }
        }

        return false;
    }

    void UpdateColorBasedOnHealth()
    {
        float t = Mathf.Clamp01(currentHealth / maxHealth);
        spriteRenderer.color = Color.Lerp(damageFadeColor, specificEnemyColor, t);
    }

    void UpdateInflationBasedOnHealth()
    {
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        float damagePercent = 1f - healthPercent;
        float currentScaleMult = 1f + (damagePercent * (maxInflationMult - 1f));
        transform.localScale = targetScale * currentScaleMult;
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

        if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] Fired Shot!");

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

    void SpawnBomb() 
    { 
        if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] Dropped Bomb.");
        Instantiate(bombPrefab, transform.position, Quaternion.identity); 
    }

    void SetNewRandomDestination()
    {
        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        Vector3 targetPos = transform.position + new Vector3(randomDir.x, randomDir.y, 0);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 2.0f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    // --- PAINT LOGIC (Inner Half) ---
    void PaintTurfUnderEnemy()
    {
        if (turfTilemap == null || turfManager == null || enemyCollider == null) return;

        Bounds bounds = enemyCollider.bounds;
        Vector3 center = bounds.center;
        Vector3 innerExtents = bounds.extents * 0.5f;
        Vector3 minPos = center - innerExtents;
        Vector3 maxPos = center + innerExtents;

        Vector3Int minCell = turfTilemap.WorldToCell(minPos);
        Vector3Int maxCell = turfTilemap.WorldToCell(maxPos);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                turfManager.RegisterTile(new Vector3Int(x, y, 0), false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.gameObject.CompareTag("PlayerBullet"))
        {
            Destroy(other.gameObject);
            
            if (audioSource != null && hitSound != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(hitSound, sfxVolume);
            }

            currentHealth -= 1f;
            
            if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] Hit! HP Remaining: {currentHealth}");

            UpdateColorBasedOnHealth();
            UpdateInflationBasedOnHealth();

            if (currentHealth <= 0)
            {
                StartCoroutine(ExplosionRoutine());
            }
        }
    }

    void PlayDeathSound()
    {
        if (deathSound == null) return;

        GameObject soundObj = new GameObject("TempDeathSound");
        soundObj.transform.position = transform.position;
        
        AudioSource src = soundObj.AddComponent<AudioSource>();
        src.clip = deathSound;
        src.volume = sfxVolume;
        src.spatialBlend = 0f; 
        
        src.Play();
        Destroy(soundObj, deathSound.length + 0.1f);
    }

    IEnumerator ExplosionRoutine()
    {
        if (enableDebugLogs) Debug.Log($"[Enemy {gameObject.name}] Exploding!");
        isDead = true;
        
        if (enemyCollider != null) enemyCollider.enabled = false;
        if (agent != null) agent.enabled = false;

        if (GameManager.Instance != null) GameManager.Instance.AddKill();
                
        PlayDeathSound();
        PaintExplosionArea();

        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = targetScale * explosionPopScale; 
        Color startColor = spriteRenderer.color;

        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;

            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = newColor;

            yield return null;
        }

        Destroy(gameObject);
    }

    void PaintExplosionArea()
    {
        if (turfManager != null && turfTilemap != null)
        {
            Vector3Int centerCell = turfTilemap.WorldToCell(transform.position);

            for (int x = -deathExplosionRadius; x <= deathExplosionRadius; x++)
            {
                for (int y = -deathExplosionRadius; y <= deathExplosionRadius; y++)
                {
                    if (Vector2.Distance(new Vector2(x, y), Vector2.zero) <= deathExplosionRadius)
                    {
                        Vector3Int targetPos = centerCell + new Vector3Int(x, y, 0);
                        turfManager.RegisterTile(targetPos, true);
                        turfTilemap.SetColor(targetPos, deathPaintColor);
                    }
                }
            }
        }
    }
}