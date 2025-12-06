using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed = 6f;

    private Rigidbody2D rb;

    [SerializeField] private TextMeshProUGUI MultiplierText;
    
    [Header("Combat Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed;
    [SerializeField] private float projectileRange;

    [Header("Dynamic Fire Rate")]
    [Tooltip("Time between shots when you have 0% turf (Slow)")]
    [SerializeField] private float slowFireRate = 0.5f; 
    [Tooltip("Time between shots when you have 100% turf (Fast)")]
    [SerializeField] private float fastFireRate = 0.1f;
    
    private float currentFireDelay; 
    private float timeSinceShot;

    [Header("Turf Settings")]
    [SerializeField] private TurfManager turfManager;
    [SerializeField] private Tilemap turfTilemap;
    // We don't need turfColor here anymore, the Manager handles the colors!
    private Vector3Int lastCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    
    private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;

    void Awake()
    { 
        rb = GetComponent<Rigidbody2D>();
        currentFireDelay = slowFireRate;
        
        audioSource = GetComponent<AudioSource>();
    }

    void FixedUpdate()
    {
        CheckMovementInputs();
        PaintTurfUnderPlayer();
    }

    void Update()
    {
        TurnToCursor();
        
        CalculateFireRate();

        if (timeSinceShot < currentFireDelay)
        {
            timeSinceShot += Time.deltaTime;
        }

        CheckAttackInputs();
    }

    void CalculateFireRate()
    {
        if (turfManager != null)
        {
            currentFireDelay = Mathf.Lerp(slowFireRate, fastFireRate, turfManager.GetTurfPercentage());
        }

        float bps = 1f / currentFireDelay;
        bps = Mathf.Round(bps * 10.0f) / 10.0f; // Round to 1 decimal place

        MultiplierText.text = bps + " p/s";
    }

    void CheckMovementInputs()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;

        input = input.normalized * speed;
        rb.linearVelocity = input;
    }

    void CheckAttackInputs()
    {
        if (Mouse.current.leftButton.isPressed && timeSinceShot >= currentFireDelay)
        {
            GameObject projectile = Instantiate(
                projectilePrefab,
                transform.position + transform.right * 0.5f,
                transform.rotation
            );
        
            if (audioSource != null && shootSound != null)
            {
                // 1. Calculate how "fast" we are firing (0.0 is slow, 1.0 is max speed)
                // We use slowFireRate (e.g. 0.5) and fastFireRate (e.g. 0.1) from your variables
                float fireIntensity = Mathf.InverseLerp(slowFireRate, fastFireRate, currentFireDelay);

                // 2. Set the base pitch: 
                // Low fire rate = 0.9 pitch (slightly deep)
                // Max fire rate = 1.4 pitch (higher, more energetic)
                float basePitch = Mathf.Lerp(0.8f, 1.4f, fireIntensity);

                // 3. Add random variance (-0.1 to +0.1) so no two shots sound identical
                float randomVariance = Random.Range(-0.1f, 0.1f);

                // 4. Apply and Play
                audioSource.pitch = basePitch + randomVariance;
                audioSource.PlayOneShot(shootSound);
            }
        
            projectile.GetComponent<bulletController>().Initialise(
                true,
                projectileSpeed,
                5f,
                1f,
                0f
            );
         
            timeSinceShot = 0f;
        }
    }

    void TurnToCursor()
    {
        var mousePos = Mouse.current.position.ReadValue();
        var screenPos = new Vector3(
            mousePos.x,
            mousePos.y,
            Camera.main.WorldToScreenPoint(transform.position).z
        );

        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        transform.right = Vector3.ProjectOnPlane(world - transform.position, Vector3.forward).normalized;
    }

    void PaintTurfUnderPlayer()
    {
        if (turfTilemap == null || turfManager == null) return;

        Vector3Int cellPos = turfTilemap.WorldToCell(transform.position);
        
        if (cellPos == lastCell) return;

        // Delegate all logic to the Manager (Painting + Scoring)
        // Pass 'true' because this is the Player
        turfManager.RegisterTile(cellPos, true);

        lastCell = cellPos;
    }
}