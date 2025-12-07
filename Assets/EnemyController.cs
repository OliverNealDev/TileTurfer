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
    private bool isGrowing = true; 
    private Vector3Int lastCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    [Header("Variation Settings")]
    [SerializeField] private float minSizeMult = 0.85f;
    [SerializeField] private float maxSizeMult = 1.15f;
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float colorVariance = 0.1f;
    
    [Tooltip("Time in seconds to reach full size")]
    [SerializeField] private float growthDuration = 0.5f; 
    private float growthTimer = 0f;
    
    private Vector3 targetScale; // The "Base" size of this specific enemy
    private Color specificEnemyColor;

    [Header("Health & Damage")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float healthRegenRate = 0.5f; 
    [SerializeField] private Color damageFadeColor = new Color(0.5f, 0f, 0.5f, 1f); 
    [SerializeField] private Color deathPaintColor = new Color(0.3f, 0f, 1f, 1f); 
    
    // --- NEW INFLATION SETTINGS ---
    [Header("Inflation & Explosion")]
    [Tooltip("How much bigger they get at 0 HP (e.g., 1.5x size)")]
    [SerializeField] private float maxInflationMult = 1.5f; 
    [SerializeField] private float explosionDuration = 0.25f;
    [SerializeField] private float explosionPopScale = 2.5f; // How big they get during the 'Pop' frame
    [SerializeField] private int deathExplosionRadius = 2;
    
    private float currentHealth;
    private bool isDead = false; // To stop logic during explosion animation

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
        if (isDead) return; // Stop update loop if dying
        if (agent == null || playerObj == null) return;
        
        // --- 1. HANDLE INITIAL SPAWN GROWTH ---
        if (isGrowing)
        {
            growthTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growthTimer / growthDuration);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            if (growthTimer >= growthDuration)
            {
                isGrowing = false;
                transform.localScale = targetScale;
            }
        }
        else
        {
            // --- 2. HANDLE HEALTH, REGEN & INFLATION ---
            if (currentHealth < maxHealth)
            {
                currentHealth += healthRegenRate * Time.deltaTime;
                if (currentHealth > maxHealth) currentHealth = maxHealth;
            }

            // Visual Updates
            UpdateColorBasedOnHealth();
            UpdateInflationBasedOnHealth();
        }

        // --- 3. MOVEMENT & LOGIC ---
        
        HandleBombSpawning();
            
        float distanceToPlayer = Vector2.Distance(transform.position, playerObj.transform.position);

        if (distanceToPlayer <= 5f)
        {
            agent.SetDestination(playerObj.transform.position);
            
            if (angrySprite != null) spriteRenderer.sprite = angrySprite;

            Vector3 direction = (playerObj.transform.position - transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            if (!isGrowing)
            {
                HandleShooting(); 
            }
        }
        else
        {
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

    void UpdateColorBasedOnHealth()
    {
        float t = Mathf.Clamp01(currentHealth / maxHealth);
        spriteRenderer.color = Color.Lerp(damageFadeColor, specificEnemyColor, t);
    }

    void UpdateInflationBasedOnHealth()
    {
        // 1.0 Health = 0 Inflation. 
        // 0.0 Health = Max Inflation.
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        float damagePercent = 1f - healthPercent;

        // Linear interpolation for scale multiplier: 1.0 -> maxInflationMult
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
        if (isDead) return;

        if (other.gameObject.CompareTag("PlayerBullet"))
        {
            Destroy(other.gameObject);
            
            if (audioSource != null && hitSound != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(hitSound, sfxVolume);
            }

            // Damage Logic
            currentHealth -= 1f;

            // Updates happen next frame in Update(), but we can force visuals here if needed
            UpdateColorBasedOnHealth();
            UpdateInflationBasedOnHealth();

            if (currentHealth <= 0)
            {
                StartCoroutine(ExplosionRoutine());
            }
        }
    }

    IEnumerator ExplosionRoutine()
    {
        isDead = true;
        
        // 1. Disable Interactions
        if (enemyCollider != null) enemyCollider.enabled = false;
        if (agent != null) agent.enabled = false;

        // 2. Gameplay Logic (Score, Sound, Paint)
        if (GameManager.Instance != null) GameManager.Instance.AddKill();
                
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, sfxVolume);
        }

        PaintExplosionArea();

        // 3. Visual Pop Animation (Mirrors BombController)
        float elapsed = 0f;
        
        // Start from current inflated size
        Vector3 startScale = transform.localScale;
        // Pop to target size
        Vector3 endScale = targetScale * explosionPopScale; 
        
        Color startColor = spriteRenderer.color;

        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;

            // Expand
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            // Fade Out
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