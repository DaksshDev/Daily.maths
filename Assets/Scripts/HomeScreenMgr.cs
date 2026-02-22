using System;
using System.Collections;
using UnityEngine;
using TMPro;
using DaksshDev.Toaster;

public class HomeScreenMgr : MonoBehaviour
{
    [CoolHeader("Home Screen Manager")]

    [Space]
    [Header("References")]
    public UserGreet userGreetScript;
    public TMP_Text  streakText;
    public TMP_Text  coinsText;
    public TMP_Text  xpText;
    public UserEdit  settings;
    [SerializeField] private Progression progression;

    [Header("Streak Warning Panels")]
    [Tooltip("Panel with TMP countdown child — shows hh:mm:ss timer")]
    [SerializeField] private GameObject streakWarningPanel;
    [Tooltip("TMP child inside streakWarningPanel that shows hh:mm:ss countdown")]
    [SerializeField] private TMP_Text   streakCountdownText;
    [Tooltip("Secondary indicator panel — no text, just a visual badge/icon")]
    [SerializeField] private GameObject streakIndicatorPanel;

    [Header("Daily Challenge Panel")]
    [Tooltip("Panel shown when today's daily challenge is completed")]
    [SerializeField] private GameObject dailyChallengeNotifier;
    
    [Header("Streak Expired Panel")]
    [Tooltip("Stays visible after streak ends, until the user rebuilds a 1-day streak")]
    [SerializeField] private GameObject streakExpiredPanel;
    [Tooltip("Child TMP text inside streakExpiredPanel")]
    [SerializeField] private TMP_Text   streakExpiredText;

    [Header("Streak Expired Toast")]
    [Tooltip("Custom icon shown on the streak-ended toast")]
    [SerializeField] private Sprite streakExpiredIcon;
    [Tooltip("Tint color applied to the streak-ended toast icon")]
    [SerializeField] private Color  streakExpiredIconColor = Color.red;

    private const float CountdownTickRate = 1f;
    private Coroutine   _countdownCoroutine;

    // Toast guards — shown at most once per scene load
    private bool _streakWarningToastShown;
    private bool _streakExpiredToastShown;
    private bool _dailyToastShown;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.AddListener(OnDataLoaded);

        if (Onboarding.IsOnboarded)
            UserDataService.Instance?.FetchUserData();

        settings?.LoadUserData();

        SetPanelActive(streakWarningPanel,     false);
        SetPanelActive(streakIndicatorPanel,   false);
        SetPanelActive(dailyChallengeNotifier, false);
    }

    void OnDestroy()
    {
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.RemoveListener(OnDataLoaded);
        StopCountdown();
    }

    // ── Data callback ──────────────────────────────────────────────────────────

    private void OnDataLoaded(UserDataService.UserDataPayload data)
    {
        // Streak text uses data.streak which is already 0 if UserDataService reset it
        if (userGreetScript != null) userGreetScript.SetUsername(data.username);
        if (streakText      != null) streakText.text = FormatStreak(data.streak);
        if (coinsText       != null) coinsText.text  = data.coins + " coins";
        if (xpText          != null) xpText.text     = data.xp    + " xp";

        EvaluateStreakWarning(data);
        EvaluateDailyChallenge(data.dailyChallengeCompletedToday, data.streak);
    }

    // ── Streak warning ─────────────────────────────────────────────────────────

    private void EvaluateStreakWarning(UserDataService.UserDataPayload data)
    {
        // ── Expired ────────────────────────────────────────────────────────────
        // UserDataService sets streakExpired = true AND resets streak to 0
        if (data.streakExpired)
        {
            SetPanelActive(streakWarningPanel,   false);
            SetPanelActive(streakIndicatorPanel, false);
            StopCountdown();

            // Format the expired streak label using the last known streak value
            // We read it from PlayerPrefs here because data.streak is already 0 at this point
            int lastStreak = PlayerPrefs.GetInt("lastStreak", 0);
            if (streakExpiredText != null)
                streakExpiredText.text = $"YOUR {FormatStreakLabel(lastStreak)} HAS EXPIRED!";

            SetPanelActive(streakExpiredPanel, true);

            if (!_streakExpiredToastShown)
            {
                _streakExpiredToastShown = true;

                var config = ToastConfig.Default;
                if (streakExpiredIcon != null)
                    config = config.WithIcon(streakExpiredIcon)
                        .WithIconColor(streakExpiredIconColor);

                ToastManager.Instance?.ShowError(
                    "Your streak has ended! Start a new one today.", config);
            }
            return;
        }

// Hide the expired panel once they have an active streak again (>= 1 day)
        if (data.streak >= 1)
            SetPanelActive(streakExpiredPanel, false);

        // ── No streak at all ───────────────────────────────────────────────────
        if (data.streak <= 0)
        {
            SetPanelActive(streakWarningPanel,   false);
            SetPanelActive(streakIndicatorPanel, false);
            StopCountdown();
            return;
        }

        // ── No timestamp yet — nothing to show ─────────────────────────────────
        if (data.streakTimeRemaining <= TimeSpan.Zero)
        {
            SetPanelActive(streakWarningPanel,   false);
            SetPanelActive(streakIndicatorPanel, false);
            StopCountdown();
            return;
        }

        // ── Warning window: < 2 hours remaining ────────────────────────────────
        if (data.streakTimeRemaining.TotalHours < UserDataService.StreakWarningThresholdHours)
        {
            SetPanelActive(streakWarningPanel,   true);
            SetPanelActive(streakIndicatorPanel, true);
            StartCountdown(data.streakTimeRemaining);

            if (!_streakWarningToastShown)
            {
                _streakWarningToastShown = true;
                ToastManager.Instance?.ShowWarning("Your streak ends in less than 2 hours!");
            }
        }
        else
        {
            SetPanelActive(streakWarningPanel,   false);
            SetPanelActive(streakIndicatorPanel, false);
            StopCountdown();
        }
    }

    // ── Countdown coroutine ────────────────────────────────────────────────────

    private void StartCountdown(TimeSpan initial)
    {
        StopCountdown();
        _countdownCoroutine = StartCoroutine(CountdownRoutine(initial));
    }

    private void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    private IEnumerator CountdownRoutine(TimeSpan remaining)
    {
        while (remaining.TotalSeconds > 0)
        {
            if (streakCountdownText != null)
                streakCountdownText.text = remaining.ToString(@"hh\:mm\:ss");

            yield return new WaitForSeconds(CountdownTickRate);
            remaining = remaining.Subtract(TimeSpan.FromSeconds(CountdownTickRate));
        }

        if (streakCountdownText != null)
            streakCountdownText.text = "00:00:00";

        SetPanelActive(streakWarningPanel,   false);
        SetPanelActive(streakIndicatorPanel, false);
    }

    // ── Daily challenge ────────────────────────────────────────────────────────

    private void EvaluateDailyChallenge(bool completedToday, int streak)
    {
        if (completedToday)
        {
            SetPanelActive(dailyChallengeNotifier, true);
        }
        else
        {
            SetPanelActive(dailyChallengeNotifier, false);

            if (streak > 0 && !_dailyToastShown)
            {
                _dailyToastShown = true;
                ToastManager.Instance?.ShowNotify("Complete today's Daily Challenge to extend your streak!");
            }
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void FetchUserData() => UserDataService.Instance?.FetchUserData();

    public void RefreshStreakDisplay()
    {
        int streak = UserDataService.Instance?.Streak ?? PlayerPrefs.GetInt("streak", 0);
        if (streakText != null) streakText.text = FormatStreak(streak);
    }

    public void SetDisplayVisible(bool visible)
    {
        if (userGreetScript != null)
            userGreetScript.greetingText.gameObject.SetActive(visible);

        if (streakText != null) { streakText.gameObject.SetActive(visible); if (!visible) streakText.text = ""; }
        if (coinsText  != null) { coinsText.gameObject.SetActive(visible);  if (!visible) coinsText.text  = ""; }
        if (xpText     != null) { xpText.gameObject.SetActive(visible);     if (!visible) xpText.text     = ""; }
    }

    public void TriggerFullRefresh()
    {
        UserDataService.Instance?.RefreshHomeScreen(this, progression);
    }

    public int    GetCoins()    => UserDataService.Instance?.Coins   ?? PlayerPrefs.GetInt   ("coins",    0);
    public int    GetXP()       => UserDataService.Instance?.XP      ?? PlayerPrefs.GetInt   ("xp",       0);
    public int    GetStreak()   => UserDataService.Instance?.Streak  ?? PlayerPrefs.GetInt   ("streak",   0);
    public string GetUsername() => UserDataService.Instance?.Username ?? PlayerPrefs.GetString("username", "");

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string FormatStreak(int days)
    {
        if (days >= 365) return (days / 365) + "Y streak";
        if (days >= 30)  return (days / 30)  + "M streak";
        if (days >= 7)   return (days / 7)   + "w streak";
        return days + "d streak";
    }
    
    /// <summary>Returns e.g. "7 DAY", "3 WEEK", "2 MONTH", "1 YEAR" for use in the expired banner.</summary>
    private string FormatStreakLabel(int days)
    {
        if (days >= 365) return (days / 365) + " YEAR";
        if (days >= 30)  return (days / 30)  + " MONTH";
        if (days >= 7)   return (days / 7)   + " WEEK";
        return days + " DAY";
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}