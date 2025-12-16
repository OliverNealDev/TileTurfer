using UnityEngine;

public class PixelSnap : MonoBehaviour
{
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

        Vector3 targetPos = parent.position + parent.TransformVector(localOffset);

        float x = Mathf.Round(targetPos.x * PPU) / PPU;
        float y = Mathf.Round(targetPos.y * PPU) / PPU;

        transform.position = new Vector3(x, y, targetPos.z);
    }
}