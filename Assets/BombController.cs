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
    [Range(0f, 1f)] [SerializeField] private float beepVolume = 0.5f; // <-- NEW
    
    [SerializeField] private AudioClip explodeSound;
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 1.0f; // <-- NEW
    
    [SerializeField] private GameObject lightObject; 
    [SerializeField] private float baseBeepInterval = 1.0f; 
    [Tooltip("How long the light stays ON during a beep (in seconds)")]
    [SerializeField] private float lightFlashDuration = 0.1f;
    
    private float timeSinceLastBeep;
    private AudioSource audioSource;

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

        if (lightObject != null) lightObject.SetActive(false);
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
        // 1. Play Sound with custom Volume
        if (audioSource != null && beepSound != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f); 
            // Pass the volume variable as the second argument
            audioSource.PlayOneShot(beepSound, beepVolume);
        }

        // 2. Flash Light
        if (lightObject != null)
        {
            lightObject.SetActive(true);
            yield return new WaitForSeconds(lightFlashDuration); 
            if (!isExploding) lightObject.SetActive(false);
        }
    }

    float GetSpeedMultiplier()
    {
        float damageFactor = (float)(maxHp - currentHp);
        return 1f + (damageFactor * 1.5f);
    }

    private void OnCollisionEnter2D(Collision2D other)
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
        
        timeSinceLastBeep = 100f; 

        StartCoroutine(FlashColor());

        if (currentHp <= 0)
        {
            StartCoroutine(ExplodeSequence());
        }
    }

    IEnumerator FlashColor()
    {
        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.05f);
        spriteRenderer.color = original;
    }

    IEnumerator ExplodeSequence()
    {
        isExploding = true;
        bombCollider.enabled = false; 

        if (lightObject != null) lightObject.SetActive(false);

        if (audioSource != null && explodeSound != null)
        {
            audioSource.pitch = Random.Range(0.8f, 1.2f);
            // Pass the volume variable as the second argument
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