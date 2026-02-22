using UnityEngine;
using TMPro;

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

    
    void Start()
    {
        // Listen to the service — fires whenever data loads
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.AddListener(OnDataLoaded);

        if (Onboarding.IsOnboarded)
            UserDataService.Instance?.FetchUserData();

        settings?.LoadUserData();
    }

    void OnDestroy()
    {
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.RemoveListener(OnDataLoaded);
    }

    // Called by UserDataService event
    private void OnDataLoaded(UserDataService.UserDataPayload data)
    {
        if (userGreetScript != null) userGreetScript.SetUsername(data.username);
        if (streakText != null)      streakText.text = FormatStreak(data.streak);
        if (coinsText  != null)      coinsText.text  = data.coins + " coins";
        if (xpText     != null)      xpText.text     = data.xp    + " xp";
    }

    // Still useful to call externally (e.g. after saving streak)
    public void FetchUserData() => UserDataService.Instance?.FetchUserData();

    public void RefreshStreakDisplay()
    {
        int streak = UserDataService.Instance?.Streak ?? PlayerPrefs.GetInt("streak", 0);
        if (streakText != null) streakText.text = FormatStreak(streak);
    }

    private string FormatStreak(int days)
    {
        if (days >= 365) return (days / 365) + "Y streak";
        if (days >= 30)  return (days / 30)  + "M streak";
        if (days >= 7)   return (days / 7)   + "w streak";
        return days + "d streak";
    }
    
    /// <summary>
    /// Hides or shows all home screen text elements.
    /// When hiding, blanks text to avoid stale values flashing on restore.
    /// </summary>
    public void SetDisplayVisible(bool visible)
    {
        // Greeting
        if (userGreetScript != null)
            userGreetScript.greetingText.gameObject.SetActive(visible);

        // Stats
        if (streakText != null)
        {
            streakText.gameObject.SetActive(visible);
            if (!visible) streakText.text = "";
        }
        if (coinsText != null)
        {
            coinsText.gameObject.SetActive(visible);
            if (!visible) coinsText.text = "";
        }
        if (xpText != null)
        {
            xpText.gameObject.SetActive(visible);
            if (!visible) xpText.text = "";
        }
    }
    
    public void TriggerFullRefresh()
    {
        UserDataService.Instance?.RefreshHomeScreen(this, progression);
    }

    // Kept for Progression.cs compatibility
    public int GetCoins() => UserDataService.Instance?.Coins ?? PlayerPrefs.GetInt("coins", 0);
    public int GetXP()    => UserDataService.Instance?.XP    ?? PlayerPrefs.GetInt("xp",    0);
    public int GetStreak()=> UserDataService.Instance?.Streak?? PlayerPrefs.GetInt("streak",0);
    public string GetUsername() => UserDataService.Instance?.Username ?? PlayerPrefs.GetString("username","");
}