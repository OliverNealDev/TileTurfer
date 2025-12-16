using UnityEngine;
using UnityEngine.Tilemaps;

public class MinimapSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap worldTilemap;   
    [SerializeField] private Tilemap minimapTilemap; 
    [SerializeField] private TileBase minimapDotTile; 

    void Start()
    {
        // Optional: Wait a frame to ensure WorldMap is fully generated first
        Invoke(nameof(InitializeMinimap), 0.1f);
    }

    public void InitializeMinimap()
    {
        if (worldTilemap == null || minimapTilemap == null || minimapDotTile == null) return;

        minimapTilemap.ClearAllTiles();
        worldTilemap.CompressBounds();

        foreach (var pos in worldTilemap.cellBounds.allPositionsWithin)
        {
            if (worldTilemap.HasTile(pos))
            {
                // 1. Place the dot
                minimapTilemap.SetTile(pos, minimapDotTile);
                
                // 2. CRITICAL FIX: Unlock the flags so we can change color
                minimapTilemap.SetTileFlags(pos, TileFlags.None);

                // 3. Copy the color
                Color worldColor = worldTilemap.GetColor(pos);
                minimapTilemap.SetColor(pos, worldColor);
            }
        }
    }

    public void UpdateMinimapTile(Vector3Int pos, Color newColor)
    {
        if (minimapTilemap.HasTile(pos))
        {
            // Just in case it wasn't unlocked yet
            minimapTilemap.SetTileFlags(pos, TileFlags.None);
            minimapTilemap.SetColor(pos, newColor);
        }
    }
}