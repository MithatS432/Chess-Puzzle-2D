using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class UI : MonoBehaviour
{
    public Button playButton;
    public Button quitButton;

    public float moveDistance = 600f;
    public float animationDuration = 1.2f;

    private RectTransform playRect;
    private RectTransform quitRect;

    private CanvasGroup playGroup;
    private CanvasGroup quitGroup;

    void Start()
    {
        playRect = playButton.GetComponent<RectTransform>();
        quitRect = quitButton.GetComponent<RectTransform>();

        playGroup = playButton.GetComponent<CanvasGroup>();
        quitGroup = quitButton.GetComponent<CanvasGroup>();

        playButton.onClick.AddListener(OnPlayPressed);
        quitButton.onClick.AddListener(OnQuitPressed);
    }

    void OnPlayPressed()
    {
        playButton.interactable = false;
        quitButton.interactable = false;

        StartCoroutine(PlayAnimation());
    }

    void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator PlayAnimation()
    {
        Vector2 playStart = playRect.anchoredPosition;
        Vector2 quitStart = quitRect.anchoredPosition;

        Vector2 playTarget = playStart + Vector2.left * moveDistance;
        Vector2 quitTarget = quitStart + Vector2.right * moveDistance;

        float time = 0f;

        while (time < animationDuration)
        {
            float t = time / animationDuration;

            float bounce = Mathf.Sin(t * Mathf.PI) * 40f;

            playRect.anchoredPosition =
                Vector2.Lerp(playStart, playTarget, EaseOut(t)) + Vector2.up * bounce;

            quitRect.anchoredPosition =
                Vector2.Lerp(quitStart, quitTarget, EaseOut(t)) + Vector2.up * bounce;

            playGroup.alpha = 1f - t;
            quitGroup.alpha = 1f - t;

            time += Time.deltaTime;
            yield return null;
        }

        playGroup.alpha = 0f;
        quitGroup.alpha = 0f;

        yield return new WaitForSeconds(0.2f);

        SceneManager.LoadScene("My Puzzle World");
    }

    float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
