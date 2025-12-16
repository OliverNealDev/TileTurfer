using System.Collections;
using System.Collections.Generic;
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

    [Header("Initial Setup")]
    public bool generateFoothold = true; 
    public int initialBlobSize = 85; // Approx 9x9

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
    
    [Header("Minimap")]
    [SerializeField] private MinimapSync minimapSync;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (turfTilemap != null) 
            tilemapRenderer = turfTilemap.GetComponent<TilemapRenderer>();
    }

    void Start()
    {
        // 1. Calculate map size
        RecalculateTotalTiles();

        // 2. Generate the random Red Blob at the start only
        if (!startOwnedByEnemies && generateFoothold)
        {
            CreateInitialFoothold();
        }
    }

    void Update()
    {
        ProcessPointSoundQueue();
    }

    // --- CLUSTER GENERATOR (Runs Once at Start) ---
    void CreateInitialFoothold()
    {
        List<Vector3Int> allFloors = new List<Vector3Int>();
        foreach (var pos in turfTilemap.cellBounds.allPositionsWithin)
        {
            if (turfTilemap.HasTile(pos)) allFloors.Add(pos);
        }

        if (allFloors.Count == 0) return;

        // Pick random spot (Try to avoid center 0,0 where player is)
        Vector3Int startNode = allFloors[Random.Range(0, allFloors.Count)];
        int attempts = 0;
        while (Vector3.Distance(turfTilemap.GetCellCenterWorld(startNode), Vector3.zero) < 10f && attempts < 50)
        {
            startNode = allFloors[Random.Range(0, allFloors.Count)];
            attempts++;
        }
        
        // Grow the blob
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        HashSet<Vector3Int> paintedSet = new HashSet<Vector3Int>();
        
        frontier.Enqueue(startNode);
        paintedSet.Add(startNode);

        while (paintedSet.Count < initialBlobSize && frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            
            if (turfTilemap.GetColor(current) != enemyColor)
            {
                turfTilemap.SetColor(current, enemyColor);
                enemyTiles++;
                if (IsColorSimilar(turfTilemap.GetColor(current), playerColor)) ownedTiles--;
            }

            Vector3Int[] neighbors = {
                current + Vector3Int.up,
                current + Vector3Int.down,
                current + Vector3Int.left,
                current + Vector3Int.right
            };

            foreach (var n in neighbors)
            {
                if (turfTilemap.HasTile(n) && !paintedSet.Contains(n))
                {
                    // Random chance to create organic shape
                    if (Random.value > 0.2f) 
                    {
                        paintedSet.Add(n);
                        frontier.Enqueue(n);
                    }
                }
            }
        }
        UpdateSlider();
    }
    // ----------------------------------------------

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

    public void RecalculateTotalTiles()
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
            pointSoundQueue++; 
            if (GameManager.Instance != null) GameManager.Instance.AddTilePainted();
            
            // Win Condition
            if (IsColorSimilar(currentColor, enemyColor)) 
            {
                enemyTiles--;
                if (enemyTiles <= 0 && totalTiles > 0)
                {
                    if (GameManager.Instance != null) GameManager.Instance.TriggerVictory();
                }
            }
        }
        else
        {
            enemyTiles++;
            if (IsColorSimilar(currentColor, playerColor)) ownedTiles--;
        }

        SpawnPopEffect(cellPos, targetColor);
        turfTilemap.SetColor(cellPos, targetColor);
        
        if (minimapSync != null) minimapSync.UpdateMinimapTile(cellPos, targetColor);
        
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

        if (objTransform != null) Destroy(objTransform.gameObject);
    }

    void UpdateSlider()
    {
        if (turfSlider != null) turfSlider.value = GetTurfPercentage();
    }

    public float GetTurfPercentage()
    {
        if (totalTiles == 0) return 0f;
        return (float)ownedTiles / (float)totalTiles;
    }

    public bool IsColorSimilar(Color a, Color b)
    {
        float tolerance = 0.01f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    public Vector3? GetRandomEnemySpawnPoint(Vector3 playerPos, float safetyRadius = 8f)
    {
        List<Vector3Int> enemyPositions = new List<Vector3Int>();
        List<Vector3Int> fallbackPositions = new List<Vector3Int>();
        
        bool allowAnyTile = Time.timeSinceLevelLoad < 1.0f;

        foreach (var pos in turfTilemap.cellBounds.allPositionsWithin)
        {
            if (turfTilemap.HasTile(pos))
            {
                if (IsColorSimilar(turfTilemap.GetColor(pos), enemyColor))
                    enemyPositions.Add(pos);

                if (allowAnyTile)
                    fallbackPositions.Add(pos);
            }
        }

        if (enemyPositions.Count > 0)
        {
            Vector3Int randomCell = enemyPositions[Random.Range(0, enemyPositions.Count)];
            return turfTilemap.GetCellCenterWorld(randomCell);
        }
        
        if (allowAnyTile && fallbackPositions.Count > 0)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3Int randomCell = fallbackPositions[Random.Range(0, fallbackPositions.Count)];
                Vector3 worldPos = turfTilemap.GetCellCenterWorld(randomCell);
                if (Vector3.Distance(worldPos, playerPos) > safetyRadius) return worldPos;
            }
            return turfTilemap.GetCellCenterWorld(fallbackPositions[Random.Range(0, fallbackPositions.Count)]);
        }

        return null;
    }
}