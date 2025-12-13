using UnityEngine;

public class cameraController : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    
    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.25f;
    
    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    
    private Vector3 _currentVelocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;
        targetPosition.z = -10f; // Keep Z locked

        // Pure smooth movement, NO snapping here
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPosition, 
            ref _currentVelocity, 
            smoothTime
        );
    }
}