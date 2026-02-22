// ══════════════════════════════════════════════════════════════════════════════
//  Onboarding.cs
// ══════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;
using DaksshDev.Toaster;

public class Onboarding : MonoBehaviour
{
    public static bool IsOnboarded { get; private set; } = false;

    [CoolHeader("ONBOARDING!")]
    [Space]
    [Header("UI References")]
    public TMP_InputField UsernameInput;
    public TMP_Text       ClassDropDown;
    public GameObject     onboarding;
    public GameObject     home;
    public WelcomeDrawSeq welcomeDrawSeq;

    void Awake()
    {
        IsOnboarded = PlayerPrefs.GetInt("OnboardingComplete", 0) == 1;
    }

    void Start()
    {
        if (IsOnboarded)
        {
            if (onboarding != null) onboarding.SetActive(false);
            UserDataService.Instance?.FetchUserData();
        }
    }

    public void CompleteOnboarding()
    {
        string username  = UsernameInput?.text.Trim() ?? "";
        string userClass = ClassDropDown?.text.Trim() ?? "";

        if (string.IsNullOrEmpty(username))
        { ToastManager.Instance?.ShowError("Please enter a username!"); return; }
        if (string.IsNullOrEmpty(userClass) || userClass == "Select Class")
        { ToastManager.Instance?.ShowError("Please select a class!"); return; }

        IsOnboarded = true;
        PlayerPrefs.SetInt("OnboardingComplete", 1);
        PlayerPrefs.Save();

        UserDataService.Instance?.SaveProfile(username, userClass);
        UserDataService.Instance?.MarkOnboardingComplete();

        ToastManager.Instance?.ShowSuccess("Welcome, " + username + "!");

        if (onboarding != null) onboarding.SetActive(false);
        if (home       != null) home.SetActive(true);
        welcomeDrawSeq?.WelcomeDraw();
    }
    
}