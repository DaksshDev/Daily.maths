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
    public KeyPadInput       keyPadInput;

    [Header("Transition")]
    public HomeToPracticeTransition transition;

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
    [Tooltip("Top-level page that contains the game UI — activated when entering Practice-This mode")]
    public GameObject mainGamePage;
    public GameObject gameContainer;

    [Header("XP / Coins Display")]
    [Tooltip("The GameObject that shows XP and Coins during a session — hidden in Practice-This mode")]
    public GameObject xpCoinsDisplay;

    [Header("Completed Screen")]
    public GameObject completedScreen;
    public TMP_Text   headerText;
    public TMP_Text   totalGainedText;
    public TMP_Text   attemptInfoText;
    public TMP_Text   gainedXPText;
    public TMP_Text   gainedCoinsText;
    public GameObject rateUsObject;

    [Header("Practice-This Complete Screen")]
    [Tooltip("Shown instead of the normal completed screen when in Practice-This mode")]
    public GameObject practiceThisCompleteScreen;
    [Tooltip("Shows only the correct count number")]
    public TMP_Text   ptCorrectText;
    [Tooltip("Shows only the wrong count number")]
    public TMP_Text   ptWrongText;
    [Tooltip("Shows only the skipped count number")]
    public TMP_Text   ptSkippedText;

    [Header("Progress Bar (optional)")]
    [Tooltip("Assign a UnityEngine.UI.Slider or leave null to skip")]
    public UnityEngine.UI.Slider progressBar;

    [Header("Timing")]
    [Tooltip("Seconds to wait after a correct answer before sliding to the next question")]
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

    // Practice-This mode
    private bool   _isPracticeThis      = false;
    private string _practiceThisContent = "";

    private float _questionStartTime;

    /// <summary>Fired every time a question is loaded. (questionsCompleted, totalQuestions)</summary>
    public System.Action<int, int> OnProgressUpdated;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    void Start()
    {
        cardStartX = questionCard.anchoredPosition.x;
        answerFeedbackText.gameObject.SetActive(false);
        completedScreen.SetActive(false);

        if (practiceThisCompleteScreen != null)
            practiceThisCompleteScreen.SetActive(false);

        if (gameContainer != null) gameContainer.SetActive(false);
        questionCard.gameObject.SetActive(false);

        if (fractionQuestionParent != null)
            fractionQuestionParent.gameObject.SetActive(false);

        string lastPlayed = PlayerPrefs.GetString("LastPlayed", "");
        string today      = System.DateTime.Now.ToString("yyyy-MM-dd");
        isDaily = lastPlayed != today;

        if (!PlayerPrefs.HasKey("InstallDate"))
            PlayerPrefs.SetString("InstallDate", today);

        System.DateTime install          = System.DateTime.Parse(PlayerPrefs.GetString("InstallDate"));
        int             daysSinceInstall = (System.DateTime.Now - install).Days;
        PlayerPrefs.SetInt("DaysSinceInstall", daysSinceInstall);

        int userIQ = PlayerPrefs.GetInt("UserIQ", 5);

        questionGenerator.Init(daysSinceInstall, userIQ);
        questions = questionGenerator.GenerateQuestions(20);

        answerChecker.ResetStats();
        scoreManager.ResetSession();
        scoreManager.SetDailyMode(isDaily);

        answerChecker.OnAnswerResult += OnAnswerResult;
        questionTimer.OnTimeUp        = OnTimeUp;

        if (progressBar != null)
        {
            progressBar.minValue = 0;
            progressBar.maxValue = questions.Count;
            progressBar.value    = 0;
        }
    }

    // ==========================================================================
    //  Progress
    // ==========================================================================

    public (int completed, int total) GetProgress() =>
        (Mathf.Max(currentIndex, 0), questions != null ? questions.Count : 20);

    private void NotifyProgress(int index)
    {
        int total = questions.Count;
        OnProgressUpdated?.Invoke(index, total);

        if (progressBar != null)
            progressBar.value = index;

        if (countText != null)
            countText.text = $"{index + 1}/{total}";
    }

    // ==========================================================================
    //  Practice-This Entry Point
    // ==========================================================================

    /// <summary>
    /// Called by HelpfulInfoMgr when the user taps "Practice This" on a card.
    /// Switches the manager into Practice-This mode and starts a fresh session
    /// whose questions are themed around the card's content.
    /// </summary>
    public void StartPracticeThis(string cardContent)
    {
        _isPracticeThis      = true;
        _practiceThisContent = cardContent;

        // Hide XP / Coins UI for this mode
        if (xpCoinsDisplay != null)
            xpCoinsDisplay.SetActive(false);

        // Tell the question generator what topic to focus on
        questionGenerator.SetPracticeThisTopic(cardContent);

        // Generate a fresh batch of topical questions
        questions = questionGenerator.GenerateQuestions(20);

        answerChecker.ResetStats();
        scoreManager.ResetSession();

        // Daily mode is irrelevant here — no daily XP
        scoreManager.SetDailyMode(false);

        if (progressBar != null)
        {
            progressBar.minValue = 0;
            progressBar.maxValue = questions.Count;
            progressBar.value    = 0;
        }

        // Activate the main page and game container BEFORE StartGame so the
        // countdown GameObject is active and its coroutine can actually start.
        if (mainGamePage  != null) mainGamePage.SetActive(true);
        if (gameContainer != null) gameContainer.SetActive(true);

        // Kick off the countdown → game
        StartGame();
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

        if (keyPadInput != null) keyPadInput.SetLocked(false);

        NotifyProgress(currentIndex);
        scoreManager.RegisterAttempt();

        _questionStartTime = Time.time;
        questionTimer.StartTimer(q.timeAlloted);
    }
    
    // ==========================================================================
    //  Pause / Resume
    // ==========================================================================

    private bool _isPaused = false;
    public bool IsPaused => _isPaused;

    public void PauseGame()
    {
        if (_isPaused) return;
        _isPaused = true;

        Time.timeScale = 0f;
        questionTimer.StopTimer();
        if (keyPadInput != null) keyPadInput.SetLocked(true);
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        _isPaused      = false;
        animating      = false;

        StopAllCoroutines();

        // Reset card position
        if (questionCard != null)
            questionCard.anchoredPosition = new Vector2(cardStartX, questionCard.anchoredPosition.y);

        // Wipe session
        scoreManager.ResetSession();
        answerChecker.ResetStats();
        scoreManager.SetDailyMode(_isPracticeThis ? false : isDaily);

        // Reset progress bar
        if (progressBar != null) progressBar.value = 0;

        // Fresh questions — respect the current mode
        if (_isPracticeThis)
            questionGenerator.SetPracticeThisTopic(_practiceThisContent);

        questions    = questionGenerator.GenerateQuestions(20);
        currentIndex = -1;

        // Reset UI state
        if (completedScreen              != null) completedScreen.SetActive(false);
        if (practiceThisCompleteScreen   != null) practiceThisCompleteScreen.SetActive(false);
        if (gameContainer                != null) gameContainer.SetActive(false);
        if (questionCard                 != null) questionCard.gameObject.SetActive(false);
        if (answerFeedbackText           != null) answerFeedbackText.gameObject.SetActive(false);

        // XP display: hide in practice-this mode, restore otherwise
        if (xpCoinsDisplay != null)
            xpCoinsDisplay.SetActive(!_isPracticeThis);

        // This fires countdown → OnCountdownComplete → LoadQuestion(0)
        StartGame();
    }

    // ==========================================================================
    //  Exit Mid-Game
    // ==========================================================================

    /// <summary>
    /// Call from a pause/back button. Stops the session without rewarding anything,
    /// then returns to the home screen.
    /// </summary>
    public void ExitGame()
    {
        Time.timeScale = 1f;
        _isPaused      = false;

        questionTimer.StopTimer();
        if (keyPadInput != null) keyPadInput.SetLocked(true);

        StopAllCoroutines();

        scoreManager.ResetSession();
        answerChecker.ResetStats();

        // Restore XP display in case we exit from practice-this mode
        if (xpCoinsDisplay != null)
            xpCoinsDisplay.SetActive(true);

        // Clear practice-this state on exit
        _isPracticeThis      = false;
        _practiceThisContent = "";

        if (gameContainer                != null) gameContainer.SetActive(false);
        if (questionCard                 != null) questionCard.gameObject.SetActive(false);
        if (completedScreen              != null) completedScreen.SetActive(false);
        if (practiceThisCompleteScreen   != null) practiceThisCompleteScreen.SetActive(false);

        if (transition != null)
            transition.CloseGameScreen();
        else
            Debug.LogWarning("[GameManager] ExitGame: HomeToPracticeTransition not assigned.");
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

        if (keyPadInput != null) keyPadInput.SetLocked(true);

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
                // No unbelievable bonus in practice-this mode
                if (isUnbelievable && !_isPracticeThis)
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
        if (keyPadInput != null) keyPadInput.SetLocked(true);
        answerChecker.TrySubmitOrSkip();
        if (keyPadInput != null) keyPadInput.ForceClearDisplay();
    }

    private IEnumerator SlideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SlideToNext();
    }

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
        answerChecker.ClearInput();

        if (keyPadInput != null) keyPadInput.SetLocked(false);

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
        NotifyProgress(currentIndex);
        scoreManager.RegisterAttempt();

        _questionStartTime = Time.time;
        questionTimer.StartTimer(q.timeAlloted);
    }

    // ==========================================================================
    //  Streak / End Game
    // ==========================================================================

    private void CompleteToday()
    {
        UserDataService.Instance?.MarkDailyChallengeComplete();
        if (homeScreenMgr != null) homeScreenMgr.RefreshStreakDisplay();
    }

    private void EndGame()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");

        answerChecker.SaveWeaknessToPrefs();

        int c = answerChecker.GetCorrect();
        int w = answerChecker.GetWrong();
        int s = answerChecker.GetSkipped();

        // ── Practice-This mode: no rewards, no daily, special screen ──────────
        if (_isPracticeThis)
        {
            if (gameContainer  != null) gameContainer.SetActive(false);
            questionCard.gameObject.SetActive(false);

            if (practiceThisCompleteScreen != null)
            {
                practiceThisCompleteScreen.SetActive(true);

                if (ptCorrectText  != null) ptCorrectText.text  = c.ToString();
                if (ptWrongText    != null) ptWrongText.text    = w.ToString();
                if (ptSkippedText  != null) ptSkippedText.text  = s.ToString();
            }

            // Restore XP display for when the player returns to normal mode
            if (xpCoinsDisplay != null)
                xpCoinsDisplay.SetActive(true);

            // Clear practice-this flag so a future normal run works correctly
            _isPracticeThis      = false;
            _practiceThisContent = "";
            questionGenerator.ClearPracticeThisTopic();
            return;
        }

        // ── Normal / Daily mode ───────────────────────────────────────────────
        PlayerPrefs.SetString("LastPlayed", today);
        PlayerPrefs.Save();

        int gainedXP    = scoreManager.GetSessionXP();
        int gainedCoins = scoreManager.GetSessionCoins();
        UserDataService.Instance?.CommitSessionScore(gainedXP, gainedCoins);

        if (isDaily)
            CompleteToday();

        if (progression != null) progression.OnProgressChanged();

        if (gameContainer != null) gameContainer.SetActive(false);
        questionCard.gameObject.SetActive(false);
        completedScreen.SetActive(true);

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

        if (progressBar != null) progressBar.value = progressBar.maxValue;

        if (vfxManager != null && c >= goodResultThreshold)
            vfxManager.ShowGoodResult();
    }
}