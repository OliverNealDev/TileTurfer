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
                minimapTilemap.SetTile(pos, minimapDotTile);
                
                minimapTilemap.SetTileFlags(pos, TileFlags.None);

                Color worldColor = worldTilemap.GetColor(pos);
                minimapTilemap.SetColor(pos, worldColor);
            }
        }
    }

    public void UpdateMinimapTile(Vector3Int pos, Color newColor)
    {
        if (minimapTilemap.HasTile(pos))
        {
            minimapTilemap.SetTileFlags(pos, TileFlags.None);
            minimapTilemap.SetColor(pos, newColor);
        }
    }
}