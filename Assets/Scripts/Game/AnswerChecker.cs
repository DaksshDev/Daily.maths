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
    public Button   submitButton;

    [Header("VFX")]
    public VFXManager vfxManager;

    [Header("References")]
    [Tooltip("Assign the KeyPadInput component so we can hard-clear its display on timer expiry")]
    public KeyPadInput keyPadInput;

    private int correct;
    private int wrong;
    private int skipped;

    private Dictionary<string, int> weaknessTally = new Dictionary<string, int>();

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

    /// <summary>Soft clear — wipes the answerInput text.</summary>
    public void ClearInput()
    {
        if (answerInput != null)
            answerInput.text = "";
    }

    /// <summary>
    /// Hard clear — also nukes the KeyPadInput display directly.
    /// Call this when the timer hits 0 to prevent residual characters
    /// from a frantic keypad tap appearing in the next question.
    /// </summary>
    public void ForceHardClearInput()
    {
        ClearInput();
        if (keyPadInput != null)
            keyPadInput.ForceClearDisplay();
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
                    return;
                }
                else
                {
                    wrong++;
                    TallyWeakness();
                    if (vfxManager != null) vfxManager.WrongAnswer();
                    RegisterSkip(playVFX: false);
                    return;
                }
            }
        }

        // No (valid) input — pure skip
        ClearInput(); // ensure clean state even if unparseable text was present
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