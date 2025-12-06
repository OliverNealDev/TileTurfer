using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Rigidbody2D rb;
    [SerializeField] private TextMeshProUGUI MultiplierText;
    
    [Header("Combat Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed;
    
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 5;
    private int currentHealth;
    [SerializeField] private AudioClip hurtSound;
    private SpriteRenderer spriteRenderer;

    [Header("Dynamic Fire Rate")]
    [SerializeField] private float slowFireRate = 0.5f; 
    [SerializeField] private float fastFireRate = 0.1f;
    private float currentFireDelay; 
    private float timeSinceShot;

    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    private Vector3Int lastCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    [Header("Audio")]
    private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;

    void Awake()
    { 
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        currentFireDelay = slowFireRate;
    }

    void FixedUpdate()
    {
        CheckMovementInputs();
        PaintTurfUnderPlayer();
    }

    void Update()
    {
        TurnToCursor();
        CalculateFireRate();
        if (timeSinceShot < currentFireDelay) timeSinceShot += Time.deltaTime;
        CheckAttackInputs();
    }

    // ---------------- UPDATED COLLISION LOGIC ---------------- //

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Check for Enemy Bullet via Physical Collision
        if (other.gameObject.CompareTag("EnemyBullet"))
        {
            TakeDamage(1);
            Destroy(other.gameObject); // Destroy the bullet immediately
        }
    }

    // --------------------------------------------------------- //

    void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Player Health: " + currentHealth);

        if (audioSource != null && hurtSound != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player Died!");
        gameObject.SetActive(false);
    }

    // ---------------- EXISTING LOGIC ---------------- //

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

    void PaintTurfUnderPlayer()
    {
        if (turfTilemap == null || turfManager == null) return;
        Vector3Int cellPos = turfTilemap.WorldToCell(transform.position);
        if (cellPos == lastCell) return;
        turfManager.RegisterTile(cellPos, true);
        lastCell = cellPos;
    }
}