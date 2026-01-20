using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;


public class PuzzleManager : MonoBehaviour
{
    [Header("Background Image Rotation")]
    public Sprite[] backgroundImages;
    private int currentImageIndex = 0;
    public Image backgroundImage;
    private float timer = 0;
    private float changeInterval = 15f;

    [Header("Button Management")]
    public Button pauseButton;
    public Button homeButton;
    public Button continueButton;
    public Button quitButton;

    void Start()
    {
        if (backgroundImage == null || backgroundImages.Length == 0)
            return;

        backgroundImage.sprite = backgroundImages[currentImageIndex];
        pauseButton.onClick.AddListener(PauseGame);
        homeButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        });

        continueButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1;
            pauseButton.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(false);
            homeButton.gameObject.SetActive(false);
            quitButton.gameObject.SetActive(false);
        });
        quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
            else
            Application.Quit();
#endif
        });
    }
    void ChangeBackground()
    {
        currentImageIndex = (currentImageIndex + 1) % backgroundImages.Length;
        backgroundImage.sprite = backgroundImages[currentImageIndex];
    }
    void PauseGame()
    {
        Time.timeScale = 0;
        pauseButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(true);
        homeButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= changeInterval)
        {
            ChangeBackground();
            timer = 0;
        }
    }
}
