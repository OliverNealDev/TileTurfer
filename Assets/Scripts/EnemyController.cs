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
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    [SerializeField] private Color enemyColor;
    [Range(0f, 1f)] [SerializeField] private float paintSensitivity = 0.5f;
    private bool isGrowing = true; 
    
    [Header("NavMesh Agent Settings")]
    [SerializeField] private float agentAcceleration = 60f;
    [SerializeField] private float agentAngularSpeed = 360f;
    [SerializeField] private float navMeshSampleRadius = 3.0f;

    [Header("Pathfinding Logic")]
    [SerializeField] private float pathCalculationCooldown = 0.2f;
    [SerializeField] private int tileScanRadius = 15;
    [SerializeField] private float minMoveDistance = 4.0f;

    [Header("Tile Scoring Weights")]
    [SerializeField] private float whiteTileWeight = 10f;
    [SerializeField] private float blueTileWeight = 100f;
    [SerializeField] private float randomScoreNoise = 50f;

    [Header("Variation Settings")]
    [SerializeField] private float minSizeMult = 0.85f;
    [SerializeField] private float maxSizeMult = 1.15f;
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float colorVariance = 0.1f;
    
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

    [Header("AI & Senses")]
    [SerializeField] private float visionRange = 7f; 
    [SerializeField] private float chasePersistenceDuration = 3f;
    [SerializeField] private LayerMask obstacleMask; 
    private float lastSawPlayerTime = -10f; 

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

    [Header("Enemy Shooting (Dynamic)")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float slowFireRate = 0.5f; 
    [SerializeField] private float fastFireRate = 0.1f; 
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float burstDelay = 0.1f; 

    [Header("Milestones (Difficulty Scaling)")]
    [SerializeField] private float milestone1 = 0.25f; 
    [SerializeField] private float milestone2 = 0.50f; 
    [SerializeField] private float milestone3 = 0.75f; 

    private float currentFireDelay;
    private float fireTimer;
    private float pathCalculationTimer = 0f;
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        
        agent.acceleration = agentAcceleration; 
        agent.angularSpeed = agentAngularSpeed; 
        agent.autoBraking = false; 

        var pos = transform.position;
        transform.position = new Vector3(pos.x, pos.y, 0f);
        
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (turfManager == null) turfManager = FindAnyObjectByType<TurfManager>();
        if (turfTilemap == null) turfTilemap = GameObject.Find("TurfTilemap")?.GetComponent<Tilemap>();
        
        transform.localScale = Vector3.zero; 
        currentFireDelay = slowFireRate; 
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (!agent.isOnNavMesh) 
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, navMeshSampleRadius, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
        
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
                currentState = AIState.Roaming; 
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
        
        CalculateEnemyFireRate();
        HandleBombSpawning();
        HandleAI();
    }

    void CalculateEnemyFireRate()
    {
        if (turfManager != null)
        {
            float difficulty = turfManager.GetTurfPercentage(); 
            currentFireDelay = Mathf.Lerp(slowFireRate, fastFireRate, difficulty);
        }
    }

    void HandleAI()
    {
        if (isGrowing) return;

        bool canSeePlayer = CheckLineOfSight();

        if (canSeePlayer)
        {
            lastSawPlayerTime = Time.time;
            currentState = AIState.ChasingVisible;
        }

        bool shouldChase = Time.time - lastSawPlayerTime < chasePersistenceDuration;

        if (shouldChase)
        {
            if (!canSeePlayer) currentState = AIState.ChasingMemory;
            agent.SetDestination(playerObj.transform.position);
            
            if (angrySprite != null) spriteRenderer.sprite = angrySprite;

            Vector3 direction = (playerObj.transform.position - transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            if (canSeePlayer) HandleShooting();
        }
        else
        {
            currentState = AIState.Roaming;
            if (neutralSprite != null) spriteRenderer.sprite = neutralSprite;

            if (agent.velocity.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(agent.velocity.y, agent.velocity.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * (rotationSpeed * 0.5f));
            }

            pathCalculationTimer -= Time.deltaTime;
            if (pathCalculationTimer <= 0f)
            {
                if (!agent.hasPath || agent.remainingDistance <= 0.5f)
                {
                    SetNewRandomDestination();
                    pathCalculationTimer = pathCalculationCooldown; 
                }
            }
        }
    }

    bool CheckLineOfSight()
    {
        if (playerObj == null) return false;
        float distance = Vector2.Distance(transform.position, playerObj.transform.position);
        if (distance > visionRange) return false;

        Vector3 direction = (playerObj.transform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, visionRange, obstacleMask);

        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            return true;
        }
        return false;
    }

    void HandleShooting()
    {
        fireTimer += Time.deltaTime;

        if (fireTimer >= currentFireDelay)
        {
            SelectAndFirePattern();
            fireTimer = 0f;
        }
    }

    void SelectAndFirePattern()
    {
        float difficulty = (turfManager != null) ? turfManager.GetTurfPercentage() : 0f;

        if (difficulty >= milestone3)
        {
            StartCoroutine(FirePatternWave());
        }
        else if (difficulty >= milestone2)
        {
            StartCoroutine(FirePatternTriangle());
        }
        else if (difficulty >= milestone1)
        {
            ShootBullet(0f, 0.2f);
            ShootBullet(0f, -0.2f);
        }
        else
        {
            ShootBullet(0f, 0f);
        }
    }

    IEnumerator FirePatternTriangle()
    {
        ShootBullet(0f, 0f);
        yield return new WaitForSeconds(burstDelay);
        ShootBullet(-5f, -0.3f);
        ShootBullet(5f, 0.3f);
    }

    IEnumerator FirePatternWave()
    {
        ShootBullet(0f, 0f);
        yield return new WaitForSeconds(burstDelay);
        ShootBullet(-5f, -0.2f);
        ShootBullet(5f, 0.2f);
        yield return new WaitForSeconds(burstDelay);
        ShootBullet(-10f, -0.4f);
        ShootBullet(10f, 0.4f);
    }

    void ShootBullet(float angleOffset, float sideOffset)
    {
        if (projectilePrefab == null || playerObj == null) return;

        Vector3 spawnPos = transform.position + (transform.right * 0.5f) + (transform.up * sideOffset);
        
        Vector3 aimDir = (playerObj.transform.position - transform.position).normalized;
        float baseAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(baseAngle + angleOffset, Vector3.forward);

        GameObject bullet = Instantiate(projectilePrefab, spawnPos, rotation);

        bulletController bc = bullet.GetComponent<bulletController>();
        if (bc != null)
        {
            bc.Initialise(false, bulletSpeed, 2f, 1f, 0f, enemyCollider);
        }

        if (audioSource != null && shootSound != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(shootSound, sfxVolume * 0.5f); 
        }
    }

    void SetNewRandomDestination()
    {
        Vector3? bestTilePos = FindBestTileDestination();

        if (bestTilePos.HasValue)
        {
            agent.SetDestination(bestTilePos.Value);
        }
        else
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(5f, 10f);
            Vector3 targetPos = transform.position + new Vector3(randomDir.x, randomDir.y, 0);
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, navMeshSampleRadius, NavMesh.AllAreas)) 
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    Vector3? FindBestTileDestination()
    {
        if (turfTilemap == null || turfManager == null) return null;

        Vector3Int currentCell = turfTilemap.WorldToCell(transform.position);
        Vector3Int bestCell = Vector3Int.zero;
        float bestScore = -1f;
        bool foundAny = false;

        for (int x = -tileScanRadius; x <= tileScanRadius; x++)
        {
            for (int y = -tileScanRadius; y <= tileScanRadius; y++)
            {
                Vector3Int checkPos = currentCell + new Vector3Int(x, y, 0);
                if (!turfTilemap.HasTile(checkPos)) continue;

                Vector3 worldPos = turfTilemap.GetCellCenterWorld(checkPos);
                float distance = Vector3.Distance(transform.position, worldPos);

                if (distance < minMoveDistance) continue;

                Color tileColor = turfTilemap.GetColor(checkPos);
                bool isEnemyColor = turfManager.IsColorSimilar(tileColor, turfManager.enemyColor);
                if (isEnemyColor) continue; 

                bool isPlayerColor = turfManager.IsColorSimilar(tileColor, turfManager.playerColor);
                
                float score = 0f;

                if (isPlayerColor) score = blueTileWeight;
                else score = whiteTileWeight;

                score += UnityEngine.Random.Range(0f, randomScoreNoise);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = checkPos;
                    foundAny = true;
                }
            }
        }

        if (foundAny)
        {
            Vector3 worldTarget = turfTilemap.GetCellCenterWorld(bestCell);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(worldTarget, out hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return null;
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
        Instantiate(bombPrefab, transform.position, Quaternion.identity); 
    }

    void PaintTurfUnderEnemy()
    {
        if (turfTilemap == null || turfManager == null || enemyCollider == null) return;
        
        Bounds bounds = enemyCollider.bounds;
        Vector3 center = bounds.center;
        
        Vector3 innerExtents = bounds.extents * paintSensitivity;
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

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (isDead) return;
        if (other.gameObject.CompareTag("PlayerBullet"))
        {
            if (audioSource != null && hitSound != null) audioSource.PlayOneShot(hitSound, sfxVolume);
            currentHealth -= 1f;
            UpdateColorBasedOnHealth();
            UpdateInflationBasedOnHealth();
            if (currentHealth <= 0) StartCoroutine(ExplosionRoutine());
        }
    }

    IEnumerator ExplosionRoutine()
    {
        isDead = true;
        if (enemyCollider != null) enemyCollider.enabled = false;
        if (agent != null) agent.enabled = false;
        if (GameManager.Instance != null) GameManager.Instance.AddKill();
        
        if (deathSound != null)
        {
            GameObject tempAudio = new GameObject("TempDeathSound");
            tempAudio.transform.position = transform.position;
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.clip = deathSound;
            src.volume = sfxVolume;
            src.spatialBlend = 0f; 
            src.Play();
            Destroy(tempAudio, deathSound.length + 0.1f);
        }
        else
        {
            if(enableDebugLogs) Debug.LogWarning($"[Enemy {gameObject.name}] Death Sound is MISSING in Inspector!");
        }

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