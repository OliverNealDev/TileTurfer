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
    public string DefeatTitleText = "Game Over!";
    public Color DefeatTextColor = Color.red;
    public string VictoryTitleText = "Victory!";
    public Color VictoryTextColor = Color.green;

    public bool isGameOver = false;

    [Header("References")]
    [SerializeField] private GameOverUI gameOverUI; // Drag your UI object here

    void Awake()
    {
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
        
        if (gameOverUI != null)
        {
            gameOverUI.Setup(timeSurvived, enemiesKilled, bulletsShot, tilesPainted, bombsTriggered);
        }
    }
    
    public void TriggerVictory()
    {
        if (isGameOver) return; // Don't trigger twice
        isGameOver = true;

        if (gameOverUI != null)
        {
            gameOverUI.SetTitle("VICTORY!"); // Set title to Victory
            gameOverUI.Setup(timeSurvived, enemiesKilled, bulletsShot, tilesPainted, bombsTriggered);
        }
        
        Time.timeScale = 0f; // Optional: Pause game
    }
}