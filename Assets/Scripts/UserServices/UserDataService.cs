using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Single source of truth for all user data.
/// Pure PlayerPrefs — no Firebase dependency.
/// </summary>
public class UserDataService : MonoBehaviour
{
    public static UserDataService Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    [System.Serializable] public class UserDataEvent : UnityEvent<UserDataPayload> {}

    [Header("Events")]
    public float RefreshDelay = 0.4f;
    [Space]
    public UserDataEvent OnUserDataLoaded;
    public UnityEvent    OnUserDataSaved;
    public UnityEvent    OnOfflineLoad;

    // ── Cached Data ───────────────────────────────────────────────────────────
    public string Username   { get; private set; } = "Player";
    public string UserClass  { get; private set; } = "";
    public int    Streak     { get; private set; } = 0;
    public int    Coins      { get; private set; } = 0;
    public int    XP         { get; private set; } = 0;
    public int    Level      { get; private set; } = 0;
    public string CreatedAt  { get; private set; } = "";
    public bool   DataLoaded { get; private set; } = false;

    // ── Streak / Daily Challenge Keys ─────────────────────────────────────────
    public const  string LastStreakTimestampKey      = "lastStreakTimestamp";
    public const  string DailyChallengeDateKey       = "dailyChallengeDate";
    private const double StreakWindowHours           = 24.0;
    public  const double StreakWarningThresholdHours = 2.0;

    // ── Payload ───────────────────────────────────────────────────────────────
    [System.Serializable]
    public class UserDataPayload
    {
        public string   username;
        public string   userClass;
        public int      streak;
        public int      coins;
        public int      xp;
        public int      level;
        public string   createdAt;
        public TimeSpan streakTimeRemaining;
        public bool     dailyChallengeCompletedToday;
        /// <summary>
        /// True when a real timestamp existed but the 24h window has passed.
        /// Distinct from TimeSpan.Zero meaning "no timestamp yet".
        /// </summary>
        public bool     streakExpired;
    }

    // ==========================================================================
    //  Lifecycle
    // ==========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ==========================================================================
    //  Public API
    // ==========================================================================

    public void FetchUserData() => LoadFromPlayerPrefs();

    public void RefreshAllListeners()
    {
        if (!DataLoaded) { FetchUserData(); return; }
        FireUserDataEvent();
    }

    public void MarkOnboardingComplete()
    {
        PlayerPrefs.SetInt("OnboardingComplete", 1);
        PlayerPrefs.Save();
    }

    public void CommitSessionScore(int gainedXP, int gainedCoins)
    {
        XP    += gainedXP;
        Coins += gainedCoins;
        PlayerPrefs.SetInt("xp",    XP);
        PlayerPrefs.SetInt("coins", Coins);
        PlayerPrefs.Save();
        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    public void IncrementStreak()
    {
        Streak++;
        PlayerPrefs.SetInt("streak", Streak);
        PlayerPrefs.Save();
        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    public void SaveLevel(int level)
    {
        Level = level;
        PlayerPrefs.SetInt("currentLevel", level);
        PlayerPrefs.Save();
        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    public void SaveProfile(string username, string userClass, bool mergeToFirebase = true)
    {
        Username  = username;
        UserClass = userClass;
        PlayerPrefs.SetString("username", username);
        PlayerPrefs.SetString("class",    userClass);
        PlayerPrefs.Save();
        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    // ==========================================================================
    //  Streak & Daily Challenge
    // ==========================================================================

    /// <summary>
    /// Evaluates streak state from PlayerPrefs.
    /// If the streak has expired this resets it to 0 immediately and saves.
    /// Returns a StreakState describing what happened.
    /// </summary>
    public StreakState EvaluateStreak()
    {
        string raw         = PlayerPrefs.GetString(LastStreakTimestampKey, "");
        bool   hasStamp    = !string.IsNullOrEmpty(raw) && long.TryParse(raw, out long unix);

        if (!hasStamp)
            return new StreakState { remaining = TimeSpan.Zero, expired = false };

        DateTime resetAt  = DateTimeOffset.FromUnixTimeSeconds(
                                long.Parse(raw)).LocalDateTime.AddHours(StreakWindowHours);
        TimeSpan remaining = resetAt - DateTime.Now;

        if (remaining <= TimeSpan.Zero)
        {
            // Expired — reset streak to 0 right here in UserDataService
            if (Streak != 0)
            {
                Streak = 0;
                PlayerPrefs.SetInt("streak", 0);
                // Clear the timestamp so we don't fire expired again next load
                PlayerPrefs.DeleteKey(LastStreakTimestampKey);
                PlayerPrefs.Save();
            }
            return new StreakState { remaining = TimeSpan.Zero, expired = true };
        }

        return new StreakState { remaining = remaining, expired = false };
    }

    public struct StreakState
    {
        public TimeSpan remaining;
        public bool     expired;
    }

    /// <summary>How long until streak resets. Zero = no stamp or already expired.</summary>
    public TimeSpan GetStreakTimeRemaining() => EvaluateStreak().remaining;

    /// <summary>True when today's daily challenge has been completed.</summary>
    public bool IsDailyChallengeCompletedToday()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        return PlayerPrefs.GetString(DailyChallengeDateKey, "") == today;
    }

    /// <summary>
    /// Call after a successful daily session.
    /// Increments streak, stamps today, resets 24h window to NOW.
    /// </summary>
    public void MarkDailyChallengeComplete()
    {
        IncrementStreak();

        PlayerPrefs.SetString(DailyChallengeDateKey, DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetString(LastStreakTimestampKey, DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();

        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    // ==========================================================================
    //  Refresh / Reset
    // ==========================================================================

    public void RefreshHomeScreen(HomeScreenMgr homeScreenMgr, Progression progression)
    {
        if (homeScreenMgr == null && progression == null) return;
        StartCoroutine(RefreshHomeScreenRoutine(homeScreenMgr, progression));
    }

    private System.Collections.IEnumerator RefreshHomeScreenRoutine(
        HomeScreenMgr homeScreenMgr, Progression progression)
    {
        homeScreenMgr?.SetDisplayVisible(false);
        progression?.ResetDisplay();

        LoadFromPlayerPrefs();
        DataLoaded = false;

        yield return new UnityEngine.WaitForSeconds(RefreshDelay);

        DataLoaded = true;
        homeScreenMgr?.SetDisplayVisible(true);
        progression?.CalculateLevel();
        FireUserDataEvent();
    }

    public void ResetAndDeleteAccount(Action onComplete = null, Action<string> onError = null)
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Username = "Player"; UserClass = ""; Streak = 0;
        Coins = 0; XP = 0; Level = 0; CreatedAt = ""; DataLoaded = false;
        onComplete?.Invoke();
    }

    // ==========================================================================
    //  Internal
    // ==========================================================================

    private void LoadFromPlayerPrefs()
    {
        Username  = PlayerPrefs.GetString("username",     "Player");
        UserClass = PlayerPrefs.GetString("class",        "");
        Streak    = PlayerPrefs.GetInt   ("streak",        0);
        Coins     = PlayerPrefs.GetInt   ("coins",         0);
        XP        = PlayerPrefs.GetInt   ("xp",            0);
        Level     = PlayerPrefs.GetInt   ("currentLevel",  0);
        CreatedAt = PlayerPrefs.GetString("createdAt",    "");

        // Save lastStreak NOW before EvaluateStreak() can zero it out
        if (Streak > 0)
        {
            PlayerPrefs.SetInt("lastStreak", Streak);
            PlayerPrefs.Save();
        }

        var streakState = EvaluateStreak();

        DataLoaded = true;
        OnOfflineLoad?.Invoke();
        FireUserDataEvent(streakState);
    }

    private void FireUserDataEvent()
    {
        // Re-evaluate fresh (used by CommitSessionScore, MarkDailyChallengeComplete, etc.)
        FireUserDataEvent(EvaluateStreak());
    }

    private void FireUserDataEvent(StreakState streakState)
    {
        OnUserDataLoaded?.Invoke(new UserDataPayload
        {
            username                     = Username,
            userClass                    = UserClass,
            streak                       = Streak,
            coins                        = Coins,
            xp                           = XP,
            level                        = Level,
            createdAt                    = CreatedAt,
            streakTimeRemaining          = streakState.remaining,
            streakExpired                = streakState.expired,
            dailyChallengeCompletedToday = IsDailyChallengeCompletedToday(),
        });
    }
}