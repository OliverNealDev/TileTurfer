using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject panelObject; 
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI enemiesText;
    [SerializeField] private TextMeshProUGUI bulletsText;
    [SerializeField] private TextMeshProUGUI tilesText;
    [SerializeField] private TextMeshProUGUI bombsText;

    void Start()
    {
        if (panelObject != null) panelObject.SetActive(false);
    }

    public void SetTitle(string text)
    {
        if (titleText != null) titleText.text = text;
    }

    public void Setup(float time, int enemies, int bullets, int tiles, int bombs)
    {
        if (panelObject != null) panelObject.SetActive(true);

        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);
        timeText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);

        enemiesText.text = "Enemies Defeated: " + enemies;
        bulletsText.text = "Shots Fired: " + bullets;
        tilesText.text = "Tiles Painted: " + tiles;
        bombsText.text = "Bombs Triggered: " + bombs;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}