using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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

    // ── Payload ───────────────────────────────────────────────────────────────
    [System.Serializable]
    public class UserDataPayload
    {
        public string username;
        public string userClass;
        public int    streak;
        public int    coins;
        public int    xp;
        public int    level;
        public string createdAt;
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

    /// <summary>Load data from PlayerPrefs and fire event.</summary>
    public void FetchUserData()
    {
        LoadFromPlayerPrefs();
    }

    /// <summary>Re-fire event with cached data. Falls through to FetchUserData if cold.</summary>
    public void RefreshAllListeners()
    {
        if (!DataLoaded) { FetchUserData(); return; }
        FireUserDataEvent();
    }

    /// <summary>Called by Onboarding after completion.</summary>
    public void MarkOnboardingComplete()
    {
        PlayerPrefs.SetInt("OnboardingComplete", 1);
        PlayerPrefs.Save();
    }

    /// <summary>Add XP + Coins at end of session.</summary>
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

    /// <summary>Increment streak.</summary>
    public void IncrementStreak()
    {
        Streak++;
        PlayerPrefs.SetInt("streak", Streak);
        PlayerPrefs.Save();

        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    /// <summary>Save calculated level.</summary>
    public void SaveLevel(int level)
    {
        Level = level;
        PlayerPrefs.SetInt("currentLevel", level);
        PlayerPrefs.Save();

        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }

    /// <summary>Save username + class to PlayerPrefs.</summary>
    public void SaveProfile(string username, string userClass, bool mergeToFirebase = true)
    {
        // mergeToFirebase param kept for API compatibility but ignored
        Username  = username;
        UserClass = userClass;

        PlayerPrefs.SetString("username", username);
        PlayerPrefs.SetString("class",    userClass);
        PlayerPrefs.Save();

        OnUserDataSaved?.Invoke();
        FireUserDataEvent();
    }
    
    // ── Refresh ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hides home screen UI, waits 0.5s, re-fetches PlayerPrefs, then restores.
    /// Call this after any save that should visually "refresh" the home screen.
    /// </summary>
    public void RefreshHomeScreen(HomeScreenMgr homeScreenMgr, Progression progression)
    {
        if (homeScreenMgr == null && progression == null) return;
        StartCoroutine(RefreshHomeScreenRoutine(homeScreenMgr, progression));
    }

    private System.Collections.IEnumerator RefreshHomeScreenRoutine(
        HomeScreenMgr homeScreenMgr, Progression progression)
    {
        // 1. Hide + blank everything
        homeScreenMgr?.SetDisplayVisible(false);
        progression?.ResetDisplay();

        // 2. Re-fetch clean data in the background (no event fire yet)
        LoadFromPlayerPrefs();        // reloads all cached fields
        DataLoaded = false;           // suppress auto-broadcast mid-reset

        yield return new WaitForSeconds(RefreshDelay);

        // 3. Re-enable and push fresh data
        DataLoaded = true;
        homeScreenMgr?.SetDisplayVisible(true);
        progression?.CalculateLevel();   // recalcs + updates its own UI
        FireUserDataEvent();             // pushes username/streak/coins/xp to listeners
    }

    /// <summary>Wipe all PlayerPrefs and reset cached state.</summary>
    public void ResetAndDeleteAccount(System.Action onComplete = null, System.Action<string> onError = null)
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Username   = "Player";
        UserClass  = "";
        Streak     = 0;
        Coins      = 0;
        XP         = 0;
        Level      = 0;
        CreatedAt  = "";
        DataLoaded = false;

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

        DataLoaded = true;
        OnOfflineLoad?.Invoke();
        FireUserDataEvent();
    }

    private void FireUserDataEvent()
    {
        OnUserDataLoaded?.Invoke(new UserDataPayload
        {
            username  = Username,
            userClass = UserClass,
            streak    = Streak,
            coins     = Coins,
            xp        = XP,
            level     = Level,
            createdAt = CreatedAt,
        });
    }
}