using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private GameObject panelObject; // The visual panel (background image)
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI enemiesText;
    [SerializeField] private TextMeshProUGUI bulletsText;
    [SerializeField] private TextMeshProUGUI tilesText;
    [SerializeField] private TextMeshProUGUI bombsText;

    void Start()
    {
        // Ensure panel is hidden at start
        if (panelObject != null) panelObject.SetActive(false);
    }

    public void Setup(float time, int enemies, int bullets, int tiles, int bombs)
    {
        if (panelObject != null) panelObject.SetActive(true);

        // Format time to Minutes:Seconds
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);
        timeText.text = string.Format("Time Survived: {0:00}:{1:00}", minutes, seconds);

        enemiesText.text = "Enemies Defeated: " + enemies;
        bulletsText.text = "Shots Fired: " + bullets;
        tilesText.text = "Tiles Painted: " + tiles;
        bombsText.text = "Bombs Triggered: " + bombs;
    }

    public void RestartGame()
    {
        // Reloads the currently active scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}