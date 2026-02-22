using UnityEngine;

public class UpAndFade : MonoBehaviour
{
    [SettingsHeader("Up And Disappear")]
    
    [Space]
    public float moveDistance = 100f;
    public float moveDuration = 1f;
    [Range(0f, 1f)] public float fadeStartFraction = 0.7f;
    public float fadeDuration = 0.4f;
    public CanvasGroup canvasGroup;

    private Vector3 startPos;
    private float elapsed;
    private float fadeElapsed;
    private bool fadingStarted;

    void OnEnable()
    {
        startPos = transform.localPosition;
        elapsed = 0f;
        fadeElapsed = 0f;
        fadingStarted = false;
        if (canvasGroup) canvasGroup.alpha = 1f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float moveT = Mathf.Clamp01(elapsed / moveDuration);
        transform.localPosition = startPos + Vector3.up * moveDistance * moveT;

        if (moveT >= fadeStartFraction)
        {
            if (!fadingStarted) { fadingStarted = true; fadeElapsed = 0f; }
            fadeElapsed += Time.deltaTime;
            if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
        }
    }
}