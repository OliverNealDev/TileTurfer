using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Rigidbody2D rb;
    private Collider2D playerCollider; // Reference for bounds
    [SerializeField] private TextMeshProUGUI MultiplierText;
    
    [Header("Combat Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed;
    
    [Header("Health & Visuals")]
    [SerializeField] private float maxHealth = 5f; 
    [SerializeField] private float healthRegenRate = 1.0f;
    private float currentHealth;
    
    [SerializeField] private AudioClip hurtSound;
    
    [Tooltip("Color when at 100% Health (White)")]
    [SerializeField] private Color fullHealthColor = Color.white;
    [Tooltip("Color when at 0% Health (Red)")]
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
    
    [Tooltip("Size of the painting area relative to the player size. 0.5 = Inner Half.")]
    [Range(0.1f, 1f)] [SerializeField] private float paintSensitivity = 0.5f; // <-- NEW SETTING
    
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

        TurnToCursor();
        CalculateFireRate();
        
        if (timeSinceShot < currentFireDelay) timeSinceShot += Time.deltaTime;
        
        CheckAttackInputs();
        
        HandleHealthRegen();
        UpdateColorBasedOnHealth();
    }

    // ---------------- UPDATED PAINT LOGIC ---------------- //

    void PaintTurfUnderPlayer()
    {
        if (turfTilemap == null || turfManager == null || playerCollider == null) return;

        // 1. Get the center of the player
        Bounds bounds = playerCollider.bounds;
        Vector3 center = bounds.center;

        // 2. Calculate "Inner" extents (Half-Size * Sensitivity)
        // If Sensitivity is 0.5, we check a box half the size of the player
        Vector3 innerExtents = bounds.extents * paintSensitivity;

        // 3. Calculate Min/Max world positions for this smaller box
        Vector3 minPos = center - innerExtents;
        Vector3 maxPos = center + innerExtents;

        // 4. Convert to Grid Cells
        Vector3Int minCell = turfTilemap.WorldToCell(minPos);
        Vector3Int maxCell = turfTilemap.WorldToCell(maxPos);

        // 5. Paint
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                turfManager.RegisterTile(new Vector3Int(x, y, 0), true);
            }
        }
    }

    // ----------------------------------------------------- //

    void HandleHealthRegen()
    {
        if (currentHealth < maxHealth)
        {
            currentHealth += healthRegenRate * Time.deltaTime;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
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
        Debug.Log("Player Health: " + currentHealth);

        if (audioSource != null && hurtSound != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        UpdateColorBasedOnHealth();

        if (currentHealth <= 0)
        {
            StartCoroutine(ExplosionRoutine());
        }
    }

    void UpdateColorBasedOnHealth()
    {
        float t = Mathf.Clamp01(currentHealth / maxHealth);
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(nearDeathColor, fullHealthColor, t);
        }
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

        Debug.Log("Player Died!");
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

    void CheckAttackInputs()
    {
        if (Mouse.current.leftButton.isPressed && timeSinceShot >= currentFireDelay)
        {
            GameObject projectile = Instantiate(projectilePrefab, transform.position + transform.right * 0.5f, transform.rotation);
            if (audioSource != null && shootSound != null)
            {
                float fireIntensity = Mathf.InverseLerp(slowFireRate, fastFireRate, currentFireDelay);
                float basePitch = Mathf.Lerp(0.8f, 1.4f, fireIntensity);
                audioSource.pitch = basePitch + Random.Range(-0.1f, 0.1f);
                audioSource.PlayOneShot(shootSound);
                
                if (GameManager.Instance != null) GameManager.Instance.AddShot();
            }
            projectile.GetComponent<bulletController>().Initialise(true, projectileSpeed, 5f, 1f, 0f);
            timeSinceShot = 0f;
        }
    }

    void TurnToCursor()
    {
        var mousePos = Mouse.current.position.ReadValue();
        var screenPos = new Vector3(mousePos.x, mousePos.y, Camera.main.WorldToScreenPoint(transform.position).z);
        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        transform.right = Vector3.ProjectOnPlane(world - transform.position, Vector3.forward).normalized;
    }
}