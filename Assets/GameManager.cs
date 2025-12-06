using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Stats Tracker")]
    public float timeSurvived;
    public int enemiesKilled;
    public int bulletsShot;
    public int tilesPainted;
    public int bombsTriggered;

    public bool isGameOver = false;

    [Header("References")]
    [SerializeField] private GameOverUI gameOverUI; // Drag your UI object here

    void Awake()
    {
        // Singleton pattern to allow easy access from other scripts
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (!isGameOver)
        {
            timeSurvived += Time.deltaTime;
        }
    }

    public void AddKill() => enemiesKilled++;
    public void AddShot() => bulletsShot++;
    public void AddTilePainted() => tilesPainted++;
    public void AddBombTriggered() => bombsTriggered++;

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        
        // Show the UI
        if (gameOverUI != null)
        {
            gameOverUI.Setup(timeSurvived, enemiesKilled, bulletsShot, tilesPainted, bombsTriggered);
        }
    }
}