using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AudioSource))]
public class BombController : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHp = 3;
    private int currentHp;
    private bool isExploding = false;

    [Header("Explosion Settings")]
    [Tooltip("Radius in grid units to paint")]
    [SerializeField] private int explosionRadius = 3;
    [SerializeField] private float expansionDuration = 0.25f; 
    [SerializeField] private float targetExpansionScale = 3f;
    
    [Header("Animation Settings")]
    [SerializeField] private float baseRotateSpeed = 45f;
    [SerializeField] private float basePulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.2f; 
    private Vector3 initialScale;

    [Header("Audio & FX")]
    [SerializeField] private AudioClip beepSound;
    [Range(0f, 1f)] [SerializeField] private float beepVolume = 0.5f;
    
    [SerializeField] private AudioClip explodeSound;
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 1.0f;
    
    [Tooltip("Color to flash the sprite when beeping")]
    [SerializeField] private Color flashColor = Color.red; // <-- NEW: Flash Color
    
    [SerializeField] private float baseBeepInterval = 1.0f; 
    [Tooltip("How long the flash stays ON during a beep (in seconds)")]
    [SerializeField] private float lightFlashDuration = 0.1f;
    
    private float timeSinceLastBeep;
    private AudioSource audioSource;
    private Color originalSpriteColor; // Store original color

    [Header("References")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D bombCollider;

    void Start()
    {
        currentHp = maxHp;
        initialScale = transform.localScale;
        audioSource = GetComponent<AudioSource>();

        if (turfManager == null) turfManager = FindFirstObjectByType<TurfManager>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bombCollider == null) bombCollider = GetComponent<Collider2D>();

        // Store the initial color (e.g. White) so we can revert back to it
        if (spriteRenderer != null) originalSpriteColor = spriteRenderer.color;
    }

    void Update()
    {
        if (isExploding) return;

        HandlePassiveAnimation();
        HandleBeeping();
    }

    void HandlePassiveAnimation()
    {
        float speedMultiplier = GetSpeedMultiplier();

        transform.Rotate(Vector3.forward * baseRotateSpeed * speedMultiplier * Time.deltaTime);

        float pulse = Mathf.Sin(Time.time * basePulseSpeed * speedMultiplier) * pulseAmount;
        transform.localScale = initialScale + (Vector3.one * pulse);
    }

    void HandleBeeping()
    {
        float speedMultiplier = GetSpeedMultiplier();
        float currentInterval = baseBeepInterval / speedMultiplier;

        timeSinceLastBeep += Time.deltaTime;

        if (timeSinceLastBeep >= currentInterval)
        {
            StartCoroutine(DoBeep());
            timeSinceLastBeep = 0f;
        }
    }

    IEnumerator DoBeep()
    {
        // 1. Play Sound
        if (audioSource != null && beepSound != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f); 
            audioSource.PlayOneShot(beepSound, beepVolume);
        }

        // 2. Flash Sprite Color (Instead of Light Object)
        if (spriteRenderer != null)
        {
            spriteRenderer.color = flashColor; // Turn Red
            
            yield return new WaitForSeconds(lightFlashDuration); 
            
            if (!isExploding) 
            {
                spriteRenderer.color = originalSpriteColor; // Revert to White
            }
        }
    }

    float GetSpeedMultiplier()
    {
        float damageFactor = (float)(maxHp - currentHp);
        return 1f + (damageFactor * 1.5f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isExploding) return;

        if (other.gameObject.CompareTag("PlayerBullet"))
        {
            TakeDamage();
            Destroy(other.gameObject); 
        }
    }

    void TakeDamage()
    {
        currentHp--;
        
        timeSinceLastBeep = 100f; // Force immediate beep

        // Use the same flash logic for damage feedback
        StartCoroutine(HitFlash());

        if (currentHp <= 0)
        {
            StartCoroutine(ExplodeSequence());
        }
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white; // Bright white flash on hit
            yield return new WaitForSeconds(0.05f);
            if (!isExploding) spriteRenderer.color = originalSpriteColor;
        }
    }

    IEnumerator ExplodeSequence()
    {
        isExploding = true;
        bombCollider.enabled = false; 
        
        if (GameManager.Instance != null) GameManager.Instance.AddBombTriggered();

        // Ensure we start fading from the current color state
        if (spriteRenderer != null) spriteRenderer.color = originalSpriteColor;

        if (audioSource != null && explodeSound != null)
        {
            audioSource.pitch = Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(explodeSound, explosionVolume);
        }

        PaintExplosionArea();

        float elapsed = 0f;
        Color startColor = spriteRenderer.color;
        Vector3 startScale = transform.localScale;

        while (elapsed < expansionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expansionDuration;

            transform.localScale = Vector3.Lerp(startScale, startScale * targetExpansionScale, t);
            
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = newColor;

            yield return null;
        }

        Destroy(gameObject);
    }

    void PaintExplosionArea()
    {
        if (turfManager == null) return;

        Vector3Int centerCell = turfManager.turfTilemap.WorldToCell(transform.position);

        for (int x = -explosionRadius; x <= explosionRadius; x++)
        {
            for (int y = -explosionRadius; y <= explosionRadius; y++)
            {
                if (Vector2.Distance(new Vector2(x, y), Vector2.zero) <= explosionRadius)
                {
                    Vector3Int targetPos = centerCell + new Vector3Int(x, y, 0);
                    turfManager.RegisterTile(targetPos, false);
                }
            }
        }
    }
}