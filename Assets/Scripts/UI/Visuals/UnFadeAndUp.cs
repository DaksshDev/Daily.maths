using UnityEngine;

public class UnFadeAndUp : MonoBehaviour
{
    [SettingsHeader("Appear And Go Up")]
    
    [Space]
    public float fadeDuration = 0.4f;
    public float moveDistance = 100f;
    public float moveDuration = 1f;
    public CanvasGroup canvasGroup;

    private Vector3 startPos;
    private float elapsed;

    void OnEnable()
    {
        startPos = transform.localPosition;
        elapsed = 0f;
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        if (elapsed < fadeDuration)
        {
            if (canvasGroup) canvasGroup.alpha = elapsed / fadeDuration;
        }
        else
        {
            if (canvasGroup) canvasGroup.alpha = 1f;
            float moveT = Mathf.Clamp01((elapsed - fadeDuration) / moveDuration);
            transform.localPosition = startPos + Vector3.up * moveDistance * moveT;
        }
    }
}