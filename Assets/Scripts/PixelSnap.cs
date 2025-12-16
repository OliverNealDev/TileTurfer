using UnityEngine;

public class PixelSnap : MonoBehaviour
{
    // Adjust this to match your game's "Pixels Per Unit" (PPU) setting
    // Common values are 16, 32, or 100.
    [SerializeField] private float PPU = 16f; 

    private Transform parent;
    private Vector3 localOffset;

    void Start()
    {
        parent = transform.parent;
        localOffset = transform.localPosition;
    }

    void LateUpdate()
    {
        if (parent == null) return;

        // Calculate where we WANT to be relative to the world
        Vector3 targetPos = parent.position + parent.TransformVector(localOffset);

        // Snap that world position to the nearest pixel
        float x = Mathf.Round(targetPos.x * PPU) / PPU;
        float y = Mathf.Round(targetPos.y * PPU) / PPU;

        // Apply the snapped position
        transform.position = new Vector3(x, y, targetPos.z);
    }
}