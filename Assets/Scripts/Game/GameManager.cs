using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [CoolHeader("-Game Manager-")]
    
    [Space]
    [Header("Scripts")]
    public QuestionGenerator questionGenerator;
    public timer             questionTimer;
    public AnswerChecker     answerChecker;
    public score             scoreManager;
    public Progression       progression;
    public HomeScreenMgr     homeScreenMgr;
    public VFXManager        vfxManager;

    [Header("Question Card")]
    public RectTransform questionCard;
    public TMP_Text      questionText;
    public TMP_Text      answerFeedbackText;
    public TMP_Text      countText;

    [Header("Fraction UI")]
    [Tooltip("Parent container that holds spawned fraction prefabs (sits inside the question card)")]
    public Transform  fractionQuestionParent;
    [Tooltip("Prefab with two TMP_Text children named 'top' (numerator) and 'bottom' (denominator)")]
    public GameObject fracPrefab;
    [Tooltip("Prefab with a single TMP_Text for the operator symbol (+, -, ×, ÷)")]
    public GameObject signPrefab;

    [Header("Game Container")]
    public GameObject gameContainer;

    [Header("Completed Screen")]
    public GameObject completedScreen;
    public TMP_Text   headerText;
    public TMP_Text   totalGainedText;
    public TMP_Text   attemptInfoText;
    public TMP_Text   gainedXPText;
    public TMP_Text   gainedCoinsText;
    public GameObject rateUsObject;

    [Header("Timing")]
    [Tooltip("Seconds to wait after a correct answer before sliding to the next question — gives EarnAnim time to play")]
    public float correctAnswerDelay = 1.5f;

    [Tooltip("Minimum correct answers (out of 20) to trigger ShowGoodResult() on the end screen")]
    public int goodResultThreshold = 14;

    [Header("Countdown")]
    public countdown countdown;

    // ── State ─────────────────────────────────────────────────────────────────
    private List<Question> questions;
    private int            currentIndex = -1;
    private float          cardStartX;
    private bool           animating;
    private bool           isDaily;
    private int            _lastSkipCount;

    private float _questionStartTime;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    void Start()
    {
        cardStartX = questionCard.anchoredPosition.x;
        answerFeedbackText.gameObject.SetActive(false);
        completedScreen.SetActive(false);

        if (gameContainer != null) gameContainer.SetActive(false);
        questionCard.gameObject.SetActive(false);

        if (fractionQuestionParent != null)
            fractionQuestionParent.gameObject.SetActive(false);

        // ── Daily / install tracking ──────────────────────────────────────────
        string lastPlayed = PlayerPrefs.GetString("LastPlayed", "");
        string today      = System.DateTime.Now.ToString("yyyy-MM-dd");
        isDaily = lastPlayed != today;

        if (!PlayerPrefs.HasKey("InstallDate"))
            PlayerPrefs.SetString("InstallDate", today);

        System.DateTime install          = System.DateTime.Parse(PlayerPrefs.GetString("InstallDate"));
        int             daysSinceInstall = (System.DateTime.Now - install).Days;
        PlayerPrefs.SetInt("DaysSinceInstall", daysSinceInstall);

        int userIQ = PlayerPrefs.GetInt("UserIQ", 5);

        // ── Init generator + generate set ────────────────────────────────────
        questionGenerator.Init(daysSinceInstall, userIQ);
        questions = questionGenerator.GenerateQuestions(20);

        answerChecker.ResetStats();
        scoreManager.ResetSession();
        scoreManager.SetDailyMode(isDaily);

        answerChecker.OnAnswerResult += OnAnswerResult;
        questionTimer.OnTimeUp        = OnTimeUp;
    }

    // ==========================================================================
    //  Game Flow
    // ==========================================================================

    public void StartGame()
    {
        if (countdown != null)
            countdown.StartCountdown(OnCountdownComplete);
        else
            OnCountdownComplete();
    }

    private void OnCountdownComplete()
    {
        if (gameContainer != null) gameContainer.SetActive(true);
        questionCard.gameObject.SetActive(true);
        LoadQuestion(0);
    }

    private void LoadQuestion(int index)
    {
        if (index >= questions.Count) { EndGame(); return; }

        currentIndex = index;
        Question q   = questions[index];

        DisplayQuestion(q);

        answerChecker.CurrentAnswer = q.answer;
        answerChecker.CurrentTags   = q.tags;
        answerChecker.ClearInput();

        UpdateCountText(currentIndex);
        scoreManager.RegisterAttempt();

        _questionStartTime = Time.time;
        questionTimer.StartTimer(q.timeAlloted);
    }

    private void UpdateCountText(int index)
    {
        if (countText != null)
            countText.text = $"{index + 1}/{questions.Count}";
    }

    // ==========================================================================
    //  Fraction Display
    // ==========================================================================

    private void DisplayQuestion(Question q)
    {
        if (q.isFraction) ShowFractionQuestion(q);
        else              ShowPlainQuestion(q);
    }

    private void ShowPlainQuestion(Question q)
    {
        questionText.gameObject.SetActive(true);
        questionText.text = q.displayText;

        if (fractionQuestionParent != null)
            fractionQuestionParent.gameObject.SetActive(false);

        ClearFractionParent();
    }

    private void ShowFractionQuestion(Question q)
    {
        questionText.gameObject.SetActive(false);

        if (fractionQuestionParent == null || fracPrefab == null || signPrefab == null)
        {
            Debug.LogWarning("Fraction prefabs not assigned — falling back to plain text.");
            questionText.gameObject.SetActive(true);
            questionText.text = q.displayText;
            return;
        }

        ClearFractionParent();
        fractionQuestionParent.gameObject.SetActive(true);

        SpawnFracBlock(q.fractions[0]);
        SpawnSign(q.operatorSymbol);
        SpawnFracBlock(q.fractions[1]);
    }

    private void SpawnFracBlock(FractionData frac)
    {
        GameObject block = Instantiate(fracPrefab, fractionQuestionParent);
        foreach (var t in block.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.gameObject.name == "top")         t.text = frac.numerator.ToString();
            else if (t.gameObject.name == "bottom") t.text = frac.denominator.ToString();
        }
    }

    private void SpawnSign(string symbol)
    {
        GameObject sign = Instantiate(signPrefab, fractionQuestionParent);
        var txt = sign.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.text = symbol;
    }

    private void ClearFractionParent()
    {
        if (fractionQuestionParent == null) return;
        foreach (Transform child in fractionQuestionParent)
            Destroy(child.gameObject);
    }

    // ==========================================================================
    //  Answer Handling
    // ==========================================================================

    private void OnAnswerResult(bool correct)
    {
        questionTimer.StopTimer();

        float elapsed  = Time.time - _questionStartTime;
        float allotted = questions[currentIndex].timeAlloted;

        questionGenerator.RecordAnswer(
            questions[currentIndex].tags,
            correct,
            elapsed,
            allotted);

        if (correct)
        {
            scoreManager.RegisterCorrect(elapsed);

            if (vfxManager != null)
            {
                vfxManager.CorrectAnswer();
                bool isUnbelievable = vfxManager.ShowTimingFeedback(elapsed, allotted);
                if (isUnbelievable)
                    scoreManager.RegisterUnbelievableBonus();
            }

            StartCoroutine(SlideAfterDelay(correctAnswerDelay));
        }
        else
        {
            StartCoroutine(ShowFeedbackThenSlide());
        }
    }

    private void OnTimeUp()
    {
        answerChecker.TrySubmitOrSkip();
    }

    // ── Correct: wait then slide ──────────────────────────────────────────────

    private IEnumerator SlideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SlideToNext();
    }

    // ── Wrong / skip: brief pause then slide ─────────────────────────────────

    private IEnumerator ShowFeedbackThenSlide()
    {
        answerFeedbackText.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(1.2f);
        SlideToNext();
    }

    // ==========================================================================
    //  Slide Animation
    // ==========================================================================

    private void SlideToNext()
    {
        if (animating) return;
        StartCoroutine(SlideAnim());
    }

    private IEnumerator SlideAnim()
    {
        animating = true;
        float   duration = 0.3f;
        float   elapsed  = 0f;
        Vector2 startPos = questionCard.anchoredPosition;
        Vector2 exitPos  = new Vector2(startPos.x - 800f, startPos.y);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            questionCard.anchoredPosition =
                Vector2.Lerp(startPos, exitPos, elapsed / duration);
            yield return null;
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= questions.Count)
        {
            animating = false;
            EndGame();
            yield break;
        }

        Question q = questions[nextIndex];
        DisplayQuestion(q);
        answerChecker.CurrentAnswer = q.answer;
        answerChecker.CurrentTags   = q.tags;

        questionCard.anchoredPosition = new Vector2(startPos.x + 800f, startPos.y);

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            questionCard.anchoredPosition =
                Vector2.Lerp(new Vector2(startPos.x + 800f, startPos.y), startPos, elapsed / duration);
            yield return null;
        }

        questionCard.anchoredPosition = startPos;
        animating = false;

        currentIndex = nextIndex;
        UpdateCountText(currentIndex);
        scoreManager.RegisterAttempt();

        _questionStartTime = Time.time;
        questionTimer.StartTimer(q.timeAlloted);
    }

    // ==========================================================================
    //  Streak / End Game
    // ==========================================================================

    private void CompleteToday()
    {
        UserDataService.Instance?.IncrementStreak();
        if (homeScreenMgr != null) homeScreenMgr.RefreshStreakDisplay();
    }

    private void EndGame()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        PlayerPrefs.SetString("LastPlayed", today);
        PlayerPrefs.Save();

        answerChecker.SaveWeaknessToPrefs();

        // ── Commit score through the service ──────────────────────────────────
        int gainedXP    = scoreManager.GetSessionXP();
        int gainedCoins = scoreManager.GetSessionCoins();
        UserDataService.Instance?.CommitSessionScore(gainedXP, gainedCoins);

        if (isDaily) CompleteToday();
        if (progression != null) progression.OnProgressChanged();

        // ── UI ────────────────────────────────────────────────────────────────
        if (gameContainer != null) gameContainer.SetActive(false);
        questionCard.gameObject.SetActive(false);
        completedScreen.SetActive(true);

        int c     = answerChecker.GetCorrect();
        int w     = answerChecker.GetWrong();
        int s     = answerChecker.GetSkipped();
        int total = 20;

        headerText.text = isDaily ? "Daily Challenge Complete!" : "Practice Completed!";
        rateUsObject.SetActive(!isDaily);

        gainedXPText.text    = "+" + gainedXP    + " XP";
        gainedCoinsText.text = "+" + gainedCoins + " Coins";
        totalGainedText.text = $"Total:\nCoins: {gainedCoins}\nXP: {gainedXP}";

        string skipLine = s == 0
            ? "<color=#FFB700>Skipped: none</color>"
            : $"<color=#FFB700>Skipped: {s}</color>";

        attemptInfoText.text =
            $"<color=#30FF39>Correct: {c}/{total}</color>\n" + skipLine;

        if (vfxManager != null && c >= goodResultThreshold)
            vfxManager.ShowGoodResult();
    }
}