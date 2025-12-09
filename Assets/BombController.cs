using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AudioSource))]
public class BombController : MonoBehaviour
{
    [Header("Health & Regen")]
    [SerializeField] private float maxHp = 5f; 
    [SerializeField] private float healthRegenRate = 1.0f; 
    [SerializeField] private Color damageColor = new Color(0.6f, 0f, 0.8f, 1f); // Purple
    private float currentHp;
    private bool isExploding = false;
    
    // REMOVED: isHitFlashing variable

    [Header("Passive Paint Shooting")]
    [SerializeField] private GameObject paintBulletPrefab;
    [SerializeField] private float maxFireInterval = 3.0f;
    [SerializeField] private float minFireInterval = 0.5f;

    [Header("Explosion Settings")]
    [SerializeField] private float lifetime = 15f; 
    [SerializeField] private int explosionRadius = 3;
    [SerializeField] private float expansionDuration = 0.25f; 
    [SerializeField] private float targetExpansionScale = 3f;
    [SerializeField] private Color swellColor = new Color(0.6f, 0f, 0.8f, 1f); 
    
    [Header("Animation Settings")]
    [SerializeField] private float baseRotateSpeed = 45f;
    [SerializeField] private float shotPulseAmount = 1.2f; 
    [SerializeField] private float shotPulseDuration = 0.2f; 
    private Vector3 initialScale;

    [Header("Audio & FX")]
    [SerializeField] private AudioClip explodeSound;
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 1.0f;
    [SerializeField] private AudioClip paintShootSound;
    
    private AudioSource audioSource;
    private Color baseColor; 

    [Header("References")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D bombCollider;

    private Coroutine pulseCoroutine; 

    void Start()
    {
        currentHp = maxHp;
        initialScale = transform.localScale;
        audioSource = GetComponent<AudioSource>();

        if (turfManager == null) turfManager = FindFirstObjectByType<TurfManager>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bombCollider == null) bombCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        StartCoroutine(FizzleSequence());
        StartCoroutine(PassivePaintRoutine());
    }

    void Update()
    {
        if (isExploding) return;

        HandleHealthRegen();
        HandleVisualColor();
        HandleRotation(); 
    }

    void HandleHealthRegen()
    {
        if (currentHp < maxHp)
        {
            currentHp += healthRegenRate * Time.deltaTime;
            if (currentHp > maxHp) currentHp = maxHp;
        }
    }

    void HandleVisualColor()
    {
        if (spriteRenderer == null) return;

        // Simplified: Only calculate color based on HP (White -> Purple)
        float healthPercent = Mathf.Clamp01(currentHp / maxHp);
        
        // Lerp from damageColor (0% HP) to baseColor (100% HP)
        spriteRenderer.color = Color.Lerp(damageColor, baseColor, healthPercent);
    }

    public void TakeDamage(float damageAmount = 1f)
    {
        if (isExploding) return;

        currentHp -= damageAmount;
        
        // REMOVED: StartCoroutine(HitFlashRoutine());

        if (currentHp <= 0)
        {
            StartCoroutine(ExplodeSequence());
        }
    }

    IEnumerator PassivePaintRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (!isExploding)
        {
            ShootPaintBullet();

            float damagePercent = Mathf.Clamp01(currentHp / maxHp);
            float currentInterval = Mathf.Lerp(minFireInterval, maxFireInterval, damagePercent);

            yield return new WaitForSeconds(currentInterval);
        }
    }

    void ShootPaintBullet()
    {
        if (paintBulletPrefab == null) return;

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(ShootPulseAnimation());

        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = transform.position + (Vector3)(randomDir * 0.6f);
        GameObject paintProj = Instantiate(paintBulletPrefab, spawnPos, Quaternion.identity);
        paintProj.transform.right = randomDir;

        PaintBullet pb = paintProj.GetComponent<PaintBullet>();
        if (pb != null)
        {
            pb.Initialise(bombCollider, false); 
        }

        if (audioSource != null && paintShootSound != null)
        {
            audioSource.PlayOneShot(paintShootSound, 0.3f);
        }
    }

    void HandleRotation()
    {
        float speedMultiplier = GetSpeedMultiplier();
        transform.Rotate(Vector3.forward * baseRotateSpeed * speedMultiplier * Time.deltaTime);
    }

    IEnumerator ShootPulseAnimation()
    {
        float halfDuration = shotPulseDuration / 2f;
        float elapsed = 0f;
        
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(initialScale, initialScale * shotPulseAmount, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(initialScale * shotPulseAmount, initialScale, t);
            yield return null;
        }

        transform.localScale = initialScale;
    }

    float GetSpeedMultiplier()
    {
        float damageFactor = (maxHp - currentHp);
        return 1f + (damageFactor * 1.5f);
    }

    IEnumerator ExplodeSequence()
    {
        isExploding = true;
        bombCollider.enabled = false; 
        
        if (GameManager.Instance != null) GameManager.Instance.AddBombTriggered();
        
        if (spriteRenderer != null) spriteRenderer.color = swellColor;

        if (audioSource != null && explodeSound != null)
        {
            audioSource.pitch = Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(explodeSound, explosionVolume);
        }

        PaintExplosionArea();

        float elapsed = 0f;
        Color startColor = swellColor; 
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
                    turfManager.RegisterTile(targetPos, true);
                }
            }
        }
    }

    IEnumerator FizzleSequence()
    {
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            if (isExploding) yield break; 
            elapsed += Time.deltaTime;
            yield return null;
        }
        StartCoroutine(SafeDespawn());
    }

    IEnumerator SafeDespawn()
    {
        isExploding = true;
        bombCollider.enabled = false;
        float duration = 0.5f;
        float t = 0f;
        Vector3 startScale = transform.localScale;

        while(t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if(spriteRenderer != null) 
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                spriteRenderer.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (isExploding) return;
        if (other.gameObject.CompareTag("PlayerBullet")) TakeDamage(1f);
    }
}