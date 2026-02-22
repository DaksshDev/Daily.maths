using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class AnswerChecker : MonoBehaviour
{
    [CoolHeader("Answer Validator")]
    
    [Space]
    [Header("Stats Display")]
    public TMP_Text correctText;
    public TMP_Text wrongText;
    public TMP_Text skippedText;

    [Header("Answer Input")]
    public TMP_Text answerInput;
    public Button submitButton;

    [Header("VFX")]
    public VFXManager vfxManager;

    private int correct;
    private int wrong;
    private int skipped;

    private Dictionary<string, int> weaknessTally = new Dictionary<string, int>();
    private float _submitTime;

    public System.Action<bool> OnAnswerResult;

    void Start()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(Submit);
    }

    // ── Public reset ──────────────────────────────────────────────────────────

    public void ResetStats()
    {
        correct = 0; wrong = 0; skipped = 0;
        weaknessTally.Clear();
        UpdateUI();
    }

    public void ClearInput()
    {
        if (answerInput != null)
            answerInput.text = "";
    }

    // ── Input Parsing ─────────────────────────────────────────────────────────

    private bool TryParseInput(string input, out float result)
    {
        result = 0f;
        input  = input.Trim();

        if (input.Contains("/"))
        {
            string[] parts = input.Split('/');
            if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), out int num)
                && int.TryParse(parts[1].Trim(), out int den)
                && den != 0)
            {
                result = (float)num / den;
                return true;
            }
            return false;
        }

        return float.TryParse(input,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }

    private bool IsCorrect(float userAnswer, float correctAnswer)
    {
        float u = Mathf.Round(userAnswer    * 1000f) / 1000f;
        float c = Mathf.Round(correctAnswer * 1000f) / 1000f;
        return Mathf.Abs(u - c) < 0.001f;
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    public void Submit()
    {
        if (answerInput == null || string.IsNullOrWhiteSpace(answerInput.text)) return;

        if (!TryParseInput(answerInput.text, out float userAnswer))
        {
            Debug.Log("[AnswerChecker] Could not parse input — ignoring submit.");
            return;
        }

        bool isCorrect = IsCorrect(userAnswer, CurrentAnswer);
        _submitTime    = Time.time;

        ClearInput();

        if (isCorrect)
        {
            correct++;
        }
        else
        {
            wrong++;
            TallyWeakness();
            if (vfxManager != null) vfxManager.WrongAnswer();
        }

        UpdateUI();
        OnAnswerResult?.Invoke(isCorrect);
    }

    // ── Timer expiry ──────────────────────────────────────────────────────────

    public void TrySubmitOrSkip()
    {
        if (answerInput != null && !string.IsNullOrWhiteSpace(answerInput.text))
        {
            if (TryParseInput(answerInput.text, out float userAnswer))
            {
                bool isCorrect = IsCorrect(userAnswer, CurrentAnswer);
                ClearInput();

                if (isCorrect)
                {
                    correct++;
                    UpdateUI();
                    OnAnswerResult?.Invoke(true);
                    // Correct VFX handled in GameManager.OnAnswerResult
                    return;
                }
                else
                {
                    // Wrong partial answer on time-up: fire WrongAnswer only,
                    // then skip the counter — but suppress SkipQuestion VFX so
                    // both effects don't fire at the same time.
                    wrong++;
                    TallyWeakness();
                    if (vfxManager != null) vfxManager.WrongAnswer();
                    RegisterSkip(playVFX: false);
                    return;
                }
            }
        }

        // No input at all — pure skip
        RegisterSkip(playVFX: true);
    }

    /// <param name="playVFX">
    /// Pass false when the VFX has already been handled upstream (e.g. a wrong
    /// answer on time-up) so SkipQuestion() doesn't fire on top of WrongAnswer().
    /// </param>
    public void RegisterSkip(bool playVFX = true)
    {
        skipped++;
        ClearInput();
        UpdateUI();

        if (playVFX && vfxManager != null) vfxManager.SkipQuestion();

        OnAnswerResult?.Invoke(false);
    }

    // ── Weakness tallying ─────────────────────────────────────────────────────

    private void TallyWeakness()
    {
        foreach (var tag in CurrentTags)
        {
            if (!weaknessTally.ContainsKey(tag)) weaknessTally[tag] = 0;
            weaknessTally[tag]++;
        }
    }

    // ── Persist to PlayerPrefs ────────────────────────────────────────────────

    public void SaveWeaknessToPrefs()
    {
        int    worstCount = 0;
        string worstTag   = "";
        foreach (var kv in weaknessTally)
        {
            if (kv.Value > worstCount)
            { worstCount = kv.Value; worstTag = kv.Key; }
        }
        PlayerPrefs.SetString("WeakTag", worstTag);

        int   total = correct + wrong + skipped;
        float ratio = total > 0 ? (float)correct / total : 0.5f;
        int   iq    = Mathf.RoundToInt(ratio * 10f);
        PlayerPrefs.SetInt("UserIQ", iq);
        PlayerPrefs.Save();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (correctText != null) correctText.text = correct.ToString();
        if (wrongText   != null) wrongText.text   = wrong.ToString();
        if (skippedText != null) skippedText.text = skipped.ToString();
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public float        CurrentAnswer { get; set; }
    public List<string> CurrentTags   { get; set; } = new List<string>();

    public int GetCorrect()  => correct;
    public int GetWrong()    => wrong;
    public int GetSkipped()  => skipped;
}