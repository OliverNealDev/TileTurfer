using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [SerializeField] private Transform targetCamera; // Drag Main Camera here
    [SerializeField] private Vector3 offset = new Vector3(-8, 4, 10);
    
    [Header("Pixel Snapping")]
    [SerializeField] private float PPU = 12f; 

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // 1. Get where the minimap "wants" to be (Smoothly following camera)
        Vector3 smoothPos = targetCamera.position + offset;

        // 2. Snap ONLY the minimap to the pixel grid
        // This stops the tiles from shimmering without making the game choppy
        float snap = 1f / PPU;
        float x = Mathf.Round(smoothPos.x / snap) * snap;
        float y = Mathf.Round(smoothPos.y / snap) * snap;

        // 3. Apply snapped position
        transform.position = new Vector3(x, y, 10f);
    }
}