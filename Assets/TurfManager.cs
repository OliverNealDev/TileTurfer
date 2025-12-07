using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class TurfManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap turfTilemap;
    [SerializeField] private Slider turfSlider;
    private TilemapRenderer tilemapRenderer; 
    
    [Header("Settings")]
    public bool startOwnedByEnemies;
    public Color playerColor = Color.green;
    public Color enemyColor = Color.red;

    [Header("Stats")]
    public int totalTiles = 0;
    public int ownedTiles = 0;
    public int enemyTiles = 0;

    [Header("Audio")]
    [SerializeField] private AudioClip pointSound;
    [SerializeField] private float pointSoundInterval = 0.05f;
    [SerializeField] private float basePitch = 1.0f; 
    [SerializeField] private float pitchVariance = 0.1f;

    [Header("Animation")]
    [SerializeField] private float popDuration = 0.3f;
    [SerializeField] private float popScale = 1.4f; 
    
    private AudioSource audioSource;
    private int pointSoundQueue = 0;
    private float pointSoundTimer = 0f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (turfTilemap != null) 
            tilemapRenderer = turfTilemap.GetComponent<TilemapRenderer>();
    }

    void Start()
    {
        RecalculateTotalTiles();
    }

    void Update()
    {
        ProcessPointSoundQueue();
    }

    void ProcessPointSoundQueue()
    {
        if (pointSoundQueue > 0)
        {
            pointSoundTimer += Time.deltaTime;

            if (pointSoundTimer >= pointSoundInterval)
            {
                PlayPointSound();
                pointSoundQueue--;
                pointSoundTimer = 0f;
            }
        }
    }

    void PlayPointSound()
    {
        if (audioSource != null && pointSound != null)
        {
            audioSource.pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
            audioSource.PlayOneShot(pointSound);
        }
    }

    void RecalculateTotalTiles()
    {
        turfTilemap.CompressBounds();
        totalTiles = 0;
        ownedTiles = 0;
        enemyTiles = 0;

        foreach (var pos in turfTilemap.cellBounds.allPositionsWithin)
        {
            if (turfTilemap.HasTile(pos))
            {
                totalTiles++;
                
                turfTilemap.SetTileFlags(pos, TileFlags.None);

                if (startOwnedByEnemies)
                {
                    turfTilemap.SetColor(pos, enemyColor);
                    enemyTiles++;
                }
                else
                {
                    Color c = turfTilemap.GetColor(pos);
                    if (IsColorSimilar(c, playerColor)) ownedTiles++;
                    else if (IsColorSimilar(c, enemyColor)) enemyTiles++;
                }
            }
        }
        
        UpdateSlider();
    }

    public void RegisterTile(Vector3Int cellPos, bool isPlayer)
    {
        if (!turfTilemap.HasTile(cellPos)) return;

        Color currentColor = turfTilemap.GetColor(cellPos);
        Color targetColor = isPlayer ? playerColor : enemyColor;

        if (IsColorSimilar(currentColor, targetColor)) return;

        if (isPlayer)
        {
            ownedTiles++;
            pointSoundQueue++; // Only play sound for player
            
            if (GameManager.Instance != null) GameManager.Instance.AddTilePainted();
            
            if (IsColorSimilar(currentColor, enemyColor))
            {
                enemyTiles--;
            }
        }
        else
        {
            enemyTiles++;

            if (IsColorSimilar(currentColor, playerColor))
            {
                ownedTiles--;
            }
        }

        // MOVED HERE: Spawns the effect for BOTH Player and Enemy
        SpawnPopEffect(cellPos, targetColor);

        turfTilemap.SetColor(cellPos, targetColor);
        
        UpdateSlider();
    }

    void SpawnPopEffect(Vector3Int cellPos, Color color)
    {
        Sprite tileSprite = turfTilemap.GetSprite(cellPos);
        if (tileSprite == null) return;

        Vector3 worldPos = turfTilemap.GetCellCenterWorld(cellPos);

        GameObject popObj = new GameObject("TilePopFX");
        popObj.transform.position = worldPos;

        SpriteRenderer sr = popObj.AddComponent<SpriteRenderer>();
        sr.sprite = tileSprite;
        sr.color = color;
        
        if (tilemapRenderer != null)
        {
            sr.sortingLayerID = tilemapRenderer.sortingLayerID;
            sr.sortingOrder = tilemapRenderer.sortingOrder + 1; 
        }

        StartCoroutine(AnimatePopObject(popObj.transform));
    }

    IEnumerator AnimatePopObject(Transform objTransform)
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.one;
        Vector3 peakScale = new Vector3(popScale, popScale, 1f);

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            
            float curve = Mathf.Sin(t * Mathf.PI);
            
            if (objTransform != null)
                objTransform.localScale = Vector3.Lerp(startScale, peakScale, curve);

            yield return null;
        }

        if (objTransform != null)
            Destroy(objTransform.gameObject);
    }

    void UpdateSlider()
    {
        if (turfSlider != null)
        {
            turfSlider.value = GetTurfPercentage();
        }
    }

    public float GetTurfPercentage()
    {
        if (totalTiles == 0) return 0f;
        return (float)ownedTiles / (float)totalTiles;
    }

    bool IsColorSimilar(Color a, Color b)
    {
        float tolerance = 0.01f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}