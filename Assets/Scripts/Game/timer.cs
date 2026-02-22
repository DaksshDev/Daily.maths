using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class timer : MonoBehaviour
{
    [CoolHeader("Timer")]
    
    [Space]
    [Header("References")]
    public Slider timerSlider;
    public TMP_Text timeRemainingText;

    [Header("Fill Image")]
    public Image fillImage; // drag the slider's Fill image here in Inspector

    // Colors to lerp between
    private static readonly Color ColorFull   = new Color(0.56f, 1f, 0.56f);         // light green  (ratio = 1)
    private static readonly Color ColorMedium = new Color(0.984f, 0.580f, 0.141f);   // #FB9424      (ratio = 0.5)
    private static readonly Color ColorLow    = new Color(1f, 0.45f, 0.45f);         // light red    (ratio = 0)

    private float totalTime;
    private float remaining;
    private bool running;
    public Action OnTimeUp;

    public void StartTimer(float seconds)
    {
        totalTime = seconds;
        remaining = seconds;
        running = true;

        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = 1f;
            timerSlider.value = 1f;
        }

        ApplyColor(1f);
    }

    public void StopTimer() => running = false;

    public float GetElapsed() => totalTime - remaining;

    void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;

        float ratio = Mathf.Clamp01(remaining / totalTime);

        if (timerSlider != null)
            timerSlider.value = ratio;

        if (timeRemainingText != null)
            timeRemainingText.text = Mathf.CeilToInt(Mathf.Max(remaining, 0f)) + "s";

        ApplyColor(ratio);

        if (remaining <= 0f)
        {
            running = false;
            OnTimeUp?.Invoke();
        }
    }

    private void ApplyColor(float ratio)
    {
        // ratio 1→0.5 : green → orange
        // ratio 0.5→0 : orange → red
        Color c = ratio > 0.5f
            ? Color.Lerp(ColorMedium, ColorFull, (ratio - 0.5f) / 0.5f)
            : Color.Lerp(ColorLow, ColorMedium, ratio / 0.5f);

        if (fillImage != null)        fillImage.color = c;
        if (timeRemainingText != null) timeRemainingText.color = c;
    }
    
    public void ResumeTimer()
    {
        if (remaining > 0f)
            running = true;  // just flip the flag — remaining is already correct
    }
}