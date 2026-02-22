using UnityEngine;
using System.Collections;

public class HomeToPracticeTransition : MonoBehaviour
{
    [SettingsHeader("Start Practice Animation")]
    
    [Space]
    public RectTransform HomeScreen;
    public GameObject GameScreen;
    public GameManager gameManager;
    private bool animating = false;

    void Start()
    {
        if (GameScreen != null && GameScreen.activeSelf)
        {
            GameScreen.SetActive(false);
        }
    }
    public void StartPractice()
    {
        StartCoroutine(SlideAnim());
    }
    
    private IEnumerator SlideAnim()
    {
        animating = true;
        GameScreen.SetActive(true);
        float duration = 0.2f;
        float elapsed = 0f;
        Vector2 startPos = HomeScreen.anchoredPosition;
        Vector2 exitPos = new Vector2(startPos.x - 1300f, startPos.y);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            HomeScreen.anchoredPosition = Vector2.Lerp(startPos, exitPos, elapsed / duration);
            yield return null;
        }
        HomeScreen.gameObject.SetActive(false);
        animating = false;
        gameManager.StartGame();
    }
}
