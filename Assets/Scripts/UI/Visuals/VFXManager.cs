using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class VFXManager : MonoBehaviour
{
    [CoolHeader("VFX Manager")]
    
    [Space]
    [Header("Scripts")]
    public VoltixCamShaker camShaker;
    public score           scoreManager;
    public AnswerChecker   answerChecker;
    public GameManager     gameManager;

    [Header("Correct Answer — Confetti")]
    public ParticleSystem correctConfetti;

    [Header("Correct Answer — Earn Animations")]
    public EarnAnim coinsEarnAnim;
    public EarnAnim xpEarnAnim;

    [Header("Correct Answer — Added Labels")]
    public TMP_Text addedCoinsText;
    public TMP_Text addedXPText;

    [Header("Wrong Answer — Flash Panel")]
    public CanvasGroup wrongFlashPanel;
    public GameObject  wrongTextObject;

    [Header("Skip — Flash Panel")]
    public CanvasGroup skipFlashPanel;
    public GameObject  skippedTextObject;

    [Header("Good Result — Confetti")]
    public ParticleSystem[] resultConfettiSystems;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   errorTickClip;
    public AudioClip   skipBoopClip;

    [Header("Bonus")]
    public GameObject bonusTextObject;
    
    [Header("Freeze Settings")]
    [Range(0.15f, 0.25f)]
    public float freezeDuration = 0.20f;

    [Header("Timing Feedback — Panel + Texts (need UnFadeAndUp component)")]
    public GameObject timingFeedbackPanel;
    public TMP_Text   timingText;
    public TMP_Text   ratingText;

    [Header("Timing Feedback — Colors")]
    public Color goodColor         = new Color(0.18f, 1f, 0.22f);
    public Color coolColor         = new Color(1f, 0.75f, 0f);
    public Color unbelievableColor = new Color(0.4f, 0.8f, 1f);

    private const float FlashInTime          = 0.05f;
    private const float FlashHoldTime        = 0.10f;
    private const float FlashOutTime         = 0.20f;
    private const float WrongShakeIntensity  = 8f;
    private const float WrongShakeDuration   = 0.25f;
    private const float SkipShakeIntensity   = 5f;
    private const float SkipShakeDuration    = 0.20f;
    private const float ResultShakeIntensity = 6f;
    private const float ResultShakeDuration  = 0.30f;

    void Awake()
    {
        if (wrongFlashPanel   != null) wrongFlashPanel.alpha  = 0f;
        if (skipFlashPanel    != null) skipFlashPanel.alpha   = 0f;
        if (wrongTextObject   != null) wrongTextObject.SetActive(false);
        if (skippedTextObject != null) skippedTextObject.SetActive(false);
        if (addedCoinsText    != null) addedCoinsText.gameObject.SetActive(false);
        if (addedXPText       != null) addedXPText.gameObject.SetActive(false);
        if (timingFeedbackPanel != null) timingFeedbackPanel.SetActive(false);
        if (timingText != null) timingText.gameObject.SetActive(false);
        if (ratingText != null) ratingText.gameObject.SetActive(false);
        if (bonusTextObject != null) bonusTextObject.SetActive(false);
    }

    // =========================================================================
    //  Public API
    // =========================================================================

    public void CorrectAnswer()
    {
        if (correctConfetti != null)
        {
            correctConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            correctConfetti.Play();
        }

        if (camShaker != null) camShaker.ShakeNormal();

        int coinsEarned = scoreManager != null ? scoreManager.GetSessionCoins() : 0;
        int xpEarned    = scoreManager != null ? scoreManager.GetSessionXP()    : 0;

        if (coinsEarnAnim != null)
            coinsEarnAnim.Play(coinsEarned, () => OnCoinsAnimComplete(coinsEarned));
        else
            Debug.LogError("[VFXManager] coinsEarnAnim is NULL — assign it in the Inspector!");

        if (xpEarnAnim != null)
            xpEarnAnim.Play(xpEarned, () => OnXPAnimComplete(xpEarned));
        else
            Debug.LogError("[VFXManager] xpEarnAnim is NULL — assign it in the Inspector!");
    }

    // Returns true if the timing qualifies as "UNBELIEVABLE"
    public bool ShowTimingFeedback(float elapsed, float allotted)
    {
        if (timingText == null || ratingText == null) return false;

        Color  tierColor;
        string ratingStr;
        bool   isUnbelievable = false;

        if (elapsed <= 2f)
        {
            tierColor     = unbelievableColor;
            ratingStr     = "UNBELIEVABLE!";
            isUnbelievable = true;
            
            // show bonus
            if (bonusTextObject != null)
            {
                bonusTextObject.SetActive(false);
                bonusTextObject.SetActive(true);
                StartCoroutine(HideAfterDelay(bonusTextObject, 1.0f));
            }
        }
        else if (elapsed <= allotted * 0.5f)
        {
            tierColor = coolColor;
            ratingStr = "Cool!";
        }
        else
        {
            tierColor = goodColor;
            ratingStr = "Good!";
        }

        string timeStr = elapsed < 10f ? $"{elapsed:F2}s" : $"{elapsed:F1}s";

        timingText.color = tierColor;
        timingText.text  = timeStr;
        ratingText.color = tierColor;
        ratingText.text  = ratingStr;

        if (timingFeedbackPanel != null) timingFeedbackPanel.SetActive(true);

        timingText.gameObject.SetActive(false);
        ratingText.gameObject.SetActive(false);
        timingText.gameObject.SetActive(true);
        ratingText.gameObject.SetActive(true);

        if (timingFeedbackPanel != null)
            StartCoroutine(HideAfterDelay(timingFeedbackPanel, 2f));

        return isUnbelievable;
    }

    public void WrongAnswer()  => StartCoroutine(WrongRoutine());
    public void SkipQuestion() => StartCoroutine(SkipRoutine());

    public void ShowGoodResult()
    {
        if (resultConfettiSystems != null)
            foreach (var ps in resultConfettiSystems)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }

        if (camShaker != null) camShaker.ShakeCustom(ResultShakeIntensity, ResultShakeDuration);
    }

    // =========================================================================
    //  Earn-Anim Callbacks
    // =========================================================================

    private void OnCoinsAnimComplete(int coins)
    {
        if (addedCoinsText == null) return;
        addedCoinsText.gameObject.SetActive(true);
        addedCoinsText.text = $"+{coins}";
        StartCoroutine(FadeOutLabel(addedCoinsText, holdSeconds: 1.5f));
    }

    private void OnXPAnimComplete(int xp)
    {
        if (addedXPText == null) return;
        addedXPText.gameObject.SetActive(true);
        addedXPText.text = $"+{xp}";
        StartCoroutine(FadeOutLabel(addedXPText, holdSeconds: 1.5f));
    }

    // =========================================================================
    //  Coroutines
    // =========================================================================

    private IEnumerator WrongRoutine()
    {
        yield return StartCoroutine(FreezeTime(freezeDuration));
        StartCoroutine(FlashPanel(wrongFlashPanel));
        if (wrongTextObject != null)
        {
            wrongTextObject.SetActive(false);
            wrongTextObject.SetActive(true);
            StartCoroutine(HideAfterDelay(wrongTextObject, 1.0f));
        }
        if (camShaker  != null) camShaker.ShakeCustom(WrongShakeIntensity, WrongShakeDuration);
        PlaySound(errorTickClip);
    }

    private IEnumerator SkipRoutine()
    {
        if (camShaker  != null) camShaker.ShakeCustom(SkipShakeIntensity, SkipShakeDuration);
        yield return StartCoroutine(FreezeTime(freezeDuration));
        if (skippedTextObject != null)
        {
            skippedTextObject.SetActive(false);
            skippedTextObject.SetActive(true);
            StartCoroutine(HideAfterDelay(skippedTextObject, 1.0f));
        }
        StartCoroutine(FlashPanel(skipFlashPanel));
        PlaySound(skipBoopClip);
    }

    private IEnumerator FreezeTime(float totalDuration)
    {
        const float FreezeScale = 0.05f;
        const float RampTime    = 0.04f;
        float       holdTime    = totalDuration - RampTime * 2f;

        float elapsed    = 0f;
        float startScale = Time.timeScale;
        while (elapsed < RampTime)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(startScale, FreezeScale, elapsed / RampTime);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdTime));

        elapsed = 0f;
        while (elapsed < RampTime)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(FreezeScale, 1f, elapsed / RampTime);
            yield return null;
        }

        Time.timeScale = 1f;
    }

    private IEnumerator FlashPanel(CanvasGroup panel)
    {
        if (panel == null) yield break;
        yield return StartCoroutine(LerpAlpha(panel, 0f, 0.6f, FlashInTime));
        yield return new WaitForSecondsRealtime(FlashHoldTime);
        yield return StartCoroutine(LerpAlpha(panel, 0.6f, 0f, FlashOutTime));
    }

    private IEnumerator LerpAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator HideAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (obj != null) obj.SetActive(false);
    }

    private IEnumerator FadeOutLabel(TMP_Text label, float holdSeconds)
    {
        yield return new WaitForSecondsRealtime(holdSeconds);
        Color original = label.color;
        float elapsed  = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            label.color = new Color(original.r, original.g, original.b,
                                    Mathf.Lerp(1f, 0f, elapsed / 0.3f));
            yield return null;
        }
        label.gameObject.SetActive(false);
        label.color = original;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}