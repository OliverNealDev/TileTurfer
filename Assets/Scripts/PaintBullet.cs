using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PaintBullet : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float checkGracePeriod = 0.1f; 
    
    [Header("Impact")]
    [SerializeField] private float knockbackForce = 5f; // New Knockback Variable
    
    [Header("Multi-Paint Settings")]
    [SerializeField] private int maxTilesToPaint = 2;
    [SerializeField] private float shrinkFactor = 0.6f; 
    
    private Collider2D parentCollider;
    private Rigidbody2D rb;
    private TurfManager turfManager;
    private Tilemap turfTilemap;
    private Collider2D myCollider;
    
    private bool isDespawning = false;
    private float spawnTime; 
    private Color targetColor; 
    
    private int tilesPainted = 0;
    private Vector3Int lastPaintedCell;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        turfManager = FindFirstObjectByType<TurfManager>();
        GameObject mapObj = GameObject.Find("TurfTilemap"); 
        if (mapObj != null) turfTilemap = mapObj.GetComponent<Tilemap>();
    }

    void Start()
    {
        spawnTime = Time.time;
        lastPaintedCell = new Vector3Int(-9999, -9999, 0);
        StartCoroutine(LifetimeTimer());
        ColorChildren();
    }

    void ColorChildren()
    {
        if (transform.childCount > 0)
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = targetColor;
    }

    public void Initialise(Collider2D shooterCollider, bool isPlayerTeam)
    {
        parentCollider = shooterCollider;
        
        if (parentCollider != null && myCollider != null)
        {
            Physics2D.IgnoreCollision(myCollider, parentCollider);
        }
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }

        if (turfManager != null)
        {
            if (isPlayerTeam)
                targetColor = turfManager.playerColor;
            else
                targetColor = turfManager.enemyColor;
        }
        
        ColorChildren();
    }

    void FixedUpdate()
    {
        if (isDespawning) return;
        if (Time.time < spawnTime + checkGracePeriod) return;
        CheckTileUnderneath();
    }

    void CheckTileUnderneath()
    {
        if (turfTilemap == null || turfManager == null) return;

        Vector3Int currentCell = turfTilemap.WorldToCell(transform.position);

        if (currentCell == lastPaintedCell) return;
        if (!turfTilemap.HasTile(currentCell)) return;
        
        Color tileColor = turfTilemap.GetColor(currentCell);

        if (!IsSimilarColor(tileColor, targetColor))
        {
            bool isPlayer = (targetColor == turfManager.playerColor);
            turfManager.RegisterTile(currentCell, isPlayer);
            
            lastPaintedCell = currentCell;
            tilesPainted++;

            if (tilesPainted >= maxTilesToPaint)
                StartCoroutine(ShrinkAndDestroy());
            else
                StartCoroutine(ShrinkStep());
        }
    }

    bool IsSimilarColor(Color a, Color b)
    {
        float tolerance = 0.02f; 
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    IEnumerator LifetimeTimer()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isDespawning) StartCoroutine(ShrinkAndDestroy());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDespawning) return;
        if (collision.collider == parentCollider) return;

        // --- NEW KNOCKBACK LOGIC ---
        Rigidbody2D hitRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (hitRb != null)
        {
            // Calculate direction from bullet to target (or just use velocity direction)
            Vector2 knockbackDir = rb.linearVelocity.normalized;
            hitRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
        }
        // ---------------------------

        if (collision.gameObject.CompareTag("Enemy"))
        {
            StartCoroutine(ShrinkAndDestroy());
            return;
        }

        Tilemap hitTilemap = collision.gameObject.GetComponent<Tilemap>();
        if (hitTilemap == null) hitTilemap = turfTilemap;

        if (hitTilemap != null && turfManager != null)
        {
            Vector3 hitPosition = Vector3.zero;
            foreach (ContactPoint2D hit in collision.contacts)
            {
                hitPosition = hit.point + (rb.linearVelocity.normalized * 0.1f);
                break;
            }
            Vector3Int cellPos = turfTilemap.WorldToCell(hitPosition);
            if (turfTilemap.HasTile(cellPos))
            {
                bool isPlayer = (targetColor == turfManager.playerColor);
                turfManager.RegisterTile(cellPos, isPlayer);
            }
            StartCoroutine(ShrinkAndDestroy());
        }
        else
        {
            StartCoroutine(ShrinkAndDestroy());
        }
    }

    IEnumerator ShrinkStep()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * shrinkFactor; 

        while (elapsed < duration)
        {
            if (isDespawning) yield break; 
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
    }

    IEnumerator ShrinkAndDestroy()
    {
        isDespawning = true;
        if (myCollider != null) myCollider.enabled = false;
        
        if (rb != null) 
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        
        SpriteRenderer sr1 = (transform.childCount > 0) ? transform.GetChild(0).GetComponent<SpriteRenderer>() : null;
        Color c1 = (sr1 != null) ? sr1.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (sr1 != null) sr1.color = new Color(c1.r, c1.g, c1.b, Mathf.Lerp(c1.a, 0f, t));
            yield return null;
        }

        Destroy(gameObject);
    }
}