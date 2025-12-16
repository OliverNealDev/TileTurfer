using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private Vector3 offset = new Vector3(-8, 4, 10);
    
    [Header("Pixel Snapping")]
    [SerializeField] private float PPU = 12f; 

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 smoothPos = targetCamera.position + offset;

        float snap = 1f / PPU;
        float x = Mathf.Round(smoothPos.x / snap) * snap;
        float y = Mathf.Round(smoothPos.y / snap) * snap;

        transform.position = new Vector3(x, y, 10f);
    }
}