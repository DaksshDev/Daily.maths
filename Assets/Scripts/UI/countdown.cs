using UnityEngine;
using TMPro;
using System.Collections;

public class countdown : MonoBehaviour
{
    [SettingsHeader("Countdown")]
    
    [Space]
    [Header("UI")]
    public TMP_Text countdownText;
    public CanvasGroup canvasGroup;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip countdownSound;
    [Range(0.5f, 2f)] public float playbackSpeed = 1f;

    [Header("Animation")]
    public float popInDuration = 0.2f;
    public float holdDuration = 1f;
    public float fadeOutDuration = 0.15f;

    private System.Action onComplete;

    public void StartCountdown(System.Action onFinished)
    {
        onComplete = onFinished;
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        int[] numbers = { 3, 2, 1 };

        if (audioSource != null && countdownSound != null)
        {
            audioSource.pitch = playbackSpeed;
            audioSource.PlayOneShot(countdownSound);
        }
        if (audioSource != null && countdownSound != null)
            audioSource.PlayOneShot(countdownSound); // play the whole clip once

        foreach (int n in numbers)
        {
            countdownText.text = n.ToString();
            yield return StartCoroutine(PopIn());
            yield return new WaitForSeconds(holdDuration);
            yield return StartCoroutine(FadeOut());
        }

        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private IEnumerator PopIn()
    {
        float elapsed = 0f;
        canvasGroup.alpha = 1f;
        countdownText.transform.localScale = Vector3.zero;

        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popInDuration;
            // Overshoot for a snappy pop-in feel
            float scale;
            if (t < 0.7f)
                scale = Mathf.Sin(t / 0.7f * Mathf.PI * 0.5f) * 1.2f;
            else
                scale = Mathf.Lerp(1.2f, 1f, (t - 0.7f) / 0.3f);

            countdownText.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        countdownText.transform.localScale = Vector3.one;
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}