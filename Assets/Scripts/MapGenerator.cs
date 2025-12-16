using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using NavMeshPlus.Components;

public class MapGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap floorTilemap; 
    [SerializeField] private Tilemap wallTilemap;  
    [SerializeField] private TileBase floorTile;   
    [SerializeField] private TileBase wallTile;
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("Map Settings")]
    public int width = 60;
    public int height = 60;
    
    [Range(0, 100)]
    public int randomFillPercent = 48; 
    public int smoothingIterations = 5; 
    
    public string seed;
    public bool useRandomSeed = true;

    private int[,] map;

    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl)) 
        {
            GenerateMap();
        }
    }

    public void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < smoothingIterations; i++)
        {
            SmoothMap();
        }

        ProcessMap();
        ClearCenter();
        DrawMap();

        Physics2D.SyncTransforms();
        
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
        
        TurfManager tm = FindFirstObjectByType<TurfManager>();
        if (tm != null)
        {
            tm.RecalculateTotalTiles(); 
        }
    }

    void ProcessMap()
    {
        List<List<Vector2Int>> floorRegions = GetRegions(0);
        
        if (floorRegions.Count == 0) return;

        List<Vector2Int> mainRoom = floorRegions[0];
        foreach (List<Vector2Int> region in floorRegions)
        {
            if (region.Count > mainRoom.Count)
            {
                mainRoom = region;
            }
        }

        foreach (List<Vector2Int> region in floorRegions)
        {
            if (region != mainRoom)
            {
                foreach (Vector2Int tile in region)
                {
                    map[tile.x, tile.y] = 1;
                }
            }
        }
    }

    List<List<Vector2Int>> GetRegions(int tileType)
    {
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Vector2Int> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Vector2Int tile in newRegion)
                    {
                        mapFlags[tile.x, tile.y] = 1;
                    }
                }
            }
        }
        return regions;
    }

    List<Vector2Int> GetRegionTiles(int startX, int startY)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.x - 1; x <= tile.x + 1; x++)
            {
                for (int y = tile.y - 1; y <= tile.y + 1; y++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        if (y == tile.y || x == tile.x)
                        {
                            if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                            {
                                mapFlags[x, y] = 1;
                                queue.Enqueue(new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }
        }
        return tiles;
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = System.DateTime.Now.Ticks.ToString();
        }

        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        int[,] newMap = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighborWallCount = GetSurroundingWallCount(x, y);

                if (neighborWallCount > 4)
                    newMap[x, y] = 1;
                else if (neighborWallCount < 4)
                    newMap[x, y] = 0;
                else
                    newMap[x, y] = map[x, y];
            }
        }
        map = newMap;
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        wallCount += map[neighborX, neighborY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    void ClearCenter()
    {
        int centerX = width / 2;
        int centerY = height / 2;
        int radius = 3;

        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    map[x, y] = 0;
                }
            }
        }
    }

    void DrawMap()
    {
        if (floorTilemap == null || wallTilemap == null) return;

        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();

        Vector3Int offset = new Vector3Int(-width / 2, -height / 2, 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0) + offset;

                if (map[x, y] == 1)
                {
                    wallTilemap.SetTile(pos, wallTile);
                }
                else
                {
                    floorTilemap.SetTile(pos, floorTile);
                }
            }
        }
    }
}