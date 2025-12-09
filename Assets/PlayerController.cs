using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Rigidbody2D rb;
    private Collider2D playerCollider; 
    [SerializeField] private TextMeshProUGUI MultiplierText;
    
    [Header("Combat Settings")]
    [SerializeField] private GameObject projectilePrefab; 
    [SerializeField] private GameObject paintBulletPrefab; 
    [SerializeField] private float projectileSpeed;
    
    [Header("Milestones")]
    [Tooltip("Unlock Backwards shot")]
    [SerializeField] private float milestone1 = 0.25f; // 25%
    [Tooltip("Unlock 4-Way shot")]
    [SerializeField] private float milestone2 = 0.50f; // 50%
    [Tooltip("Unlock 8-Way shot")]
    [SerializeField] private float milestone3 = 0.75f; // 75%

    [Header("Health & Visuals")]
    [SerializeField] private float maxHealth = 5f; 
    [SerializeField] private float healthRegenRate = 1.0f;
    private float currentHealth;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private Color fullHealthColor = Color.white;
    [SerializeField] private Color nearDeathColor = Color.red;
    private SpriteRenderer spriteRenderer;

    [Header("Death Explosion")]
    [SerializeField] private int explosionRadius = 3;
    [SerializeField] private float explosionDuration = 0.5f;
    [SerializeField] private float explosionPopScale = 2f;
    private bool isDead = false;

    [Header("Dynamic Fire Rate")]
    [SerializeField] private float slowFireRate = 0.5f; 
    [SerializeField] private float fastFireRate = 0.1f;
    private float currentFireDelay; 
    private float timeSinceShot;

    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    [Range(0.1f, 1f)] [SerializeField] private float paintSensitivity = 0.5f; 
    
    [Header("Auto Rotation (Diep.io Style)")]
    [SerializeField] private float autoRotateSpeed = 180f; // Degrees per second
    private bool isAutoRotating = false;

    [Header("Audio")]
    private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;

    void Awake()
    { 
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        currentFireDelay = slowFireRate;
        if (spriteRenderer != null) spriteRenderer.color = fullHealthColor;
    }

    void FixedUpdate()
    {
        if (isDead) return;
        CheckMovementInputs();
        PaintTurfUnderPlayer();
    }

    void Update()
    {
        if (isDead) return;

        // --- NEW AUTO ROTATE LOGIC ---
        
        // 1. Check Toggle Input
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            isAutoRotating = !isAutoRotating;
        }

        // 2. Decide Rotation Method
        if (isAutoRotating)
        {
            // Spin automatically
            transform.Rotate(Vector3.forward * autoRotateSpeed * Time.deltaTime);
        }
        else
        {
            // Use Mouse Aim
            TurnToCursor();
        }

        // -----------------------------

        CalculateFireRate();
        
        if (timeSinceShot < currentFireDelay) timeSinceShot += Time.deltaTime;
        
        CheckAttackInputs();
        HandleHealthRegen();
        UpdateColorBasedOnHealth();
    }

    // --- MILESTONE SHOOTING LOGIC ---

    void CheckAttackInputs()
    {
        // Get current turf percentage once for both checks
        float turfPercent = (turfManager != null) ? turfManager.GetTurfPercentage() : 0f;

        // 1. LEFT CLICK (Normal Bullets)
        if (Mouse.current.leftButton.isPressed && timeSinceShot >= currentFireDelay)
        {
            ShootNormal(0f); // Base

            if (turfPercent >= milestone1) ShootNormal(180f);
            if (turfPercent >= milestone2) { ShootNormal(90f); ShootNormal(-90f); }
            if (turfPercent >= milestone3) { ShootNormal(45f); ShootNormal(-45f); ShootNormal(135f); ShootNormal(-135f); }

            // Audio & Stats
            PlayShootSound();
            if (GameManager.Instance != null) GameManager.Instance.AddShot();

            timeSinceShot = 0f;
        }

        // 2. RIGHT CLICK (Paint Bullets)
        if (Mouse.current.rightButton.isPressed && timeSinceShot >= currentFireDelay)
        {
            ShootPaint(0f); // Base

            if (turfPercent >= milestone1) ShootPaint(180f);
            if (turfPercent >= milestone2) { ShootPaint(90f); ShootPaint(-90f); }
            if (turfPercent >= milestone3) { ShootPaint(45f); ShootPaint(-45f); ShootPaint(135f); ShootPaint(-135f); }
            
            if (audioSource != null && shootSound != null)
            {
                audioSource.pitch = 1.5f + Random.Range(-0.1f, 0.1f); 
                audioSource.PlayOneShot(shootSound, 0.5f);
            }

            timeSinceShot = 0f;
        }
    }

    void ShootNormal(float angleOffset)
    {
        Quaternion rotation = transform.rotation * Quaternion.Euler(0, 0, angleOffset);
        GameObject projectile = Instantiate(projectilePrefab, transform.position + (rotation * Vector3.right * 0.5f), rotation);
        
        bulletController bc = projectile.GetComponent<bulletController>();
        if (bc != null)
        {
            bc.Initialise(true, projectileSpeed, 5f, 1f, 0f, playerCollider);
        }
    }

    void ShootPaint(float angleOffset)
    {
        if (paintBulletPrefab == null) return;

        Quaternion rotation = transform.rotation * Quaternion.Euler(0, 0, angleOffset);
        GameObject paintProj = Instantiate(paintBulletPrefab, transform.position + (rotation * Vector3.right * 0.5f), rotation);
        
        PaintBullet pb = paintProj.GetComponent<PaintBullet>();
        if (pb != null)
        {
            pb.Initialise(playerCollider, true); 
        }
    }

    void PlayShootSound()
    {
        if (audioSource != null && shootSound != null)
        {
            float fireIntensity = Mathf.InverseLerp(slowFireRate, fastFireRate, currentFireDelay);
            audioSource.pitch = Mathf.Lerp(0.8f, 1.4f, fireIntensity) + Random.Range(-0.1f, 0.1f);
            audioSource.PlayOneShot(shootSound);
        }
    }

    // ------------------------------------

    void PaintTurfUnderPlayer()
    {
        if (turfTilemap == null || turfManager == null || playerCollider == null) return;
        Bounds bounds = playerCollider.bounds;
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
                turfManager.RegisterTile(new Vector3Int(x, y, 0), true);
            }
        }
    }

    void HandleHealthRegen()
    {
        if (currentHealth < maxHealth)
        {
            currentHealth += healthRegenRate * Time.deltaTime;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (isDead) return;
        if (other.gameObject.CompareTag("EnemyBullet"))
        {
            TakeDamage(1f); 
            Destroy(other.gameObject); 
        }
    }

    void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (audioSource != null && hurtSound != null) audioSource.PlayOneShot(hurtSound);
        UpdateColorBasedOnHealth();
        if (currentHealth <= 0) StartCoroutine(ExplosionRoutine());
    }

    void UpdateColorBasedOnHealth()
    {
        float t = Mathf.Clamp01(currentHealth / maxHealth);
        if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(nearDeathColor, fullHealthColor, t);
    }

    IEnumerator ExplosionRoutine()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        if (playerCollider != null) playerCollider.enabled = false;
        PaintExplosionArea();

        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * explosionPopScale;
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

        if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
        gameObject.SetActive(false);
    }

    void PaintExplosionArea()
    {
        if (turfManager != null && turfTilemap != null)
        {
            Vector3Int centerCell = turfTilemap.WorldToCell(transform.position);
            for (int x = -explosionRadius; x <= explosionRadius; x++)
            {
                for (int y = -explosionRadius; y <= explosionRadius; y++)
                {
                    if (Vector2.Distance(new Vector2(x, y), Vector2.zero) <= explosionRadius)
                    {
                        Vector3Int targetPos = centerCell + new Vector3Int(x, y, 0);
                        turfManager.RegisterTile(targetPos, true);
                    }
                }
            }
        }
    }

    void CalculateFireRate()
    {
        if (turfManager != null) currentFireDelay = Mathf.Lerp(slowFireRate, fastFireRate, turfManager.GetTurfPercentage());
        float bps = 1f / currentFireDelay;
        bps = Mathf.Round(bps * 10.0f) / 10.0f; 
        if (MultiplierText != null) MultiplierText.text = bps + " p/s";
    }

    void CheckMovementInputs()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        input = input.normalized * speed;
        rb.linearVelocity = input;
    }

    void TurnToCursor()
    {
        var mousePos = Mouse.current.position.ReadValue();
        var screenPos = new Vector3(mousePos.x, mousePos.y, Camera.main.WorldToScreenPoint(transform.position).z);
        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        transform.right = Vector3.ProjectOnPlane(world - transform.position, Vector3.forward).normalized;
    }
}