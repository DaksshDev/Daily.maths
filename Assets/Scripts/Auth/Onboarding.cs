using UnityEngine;
using TMPro;

public class Onboarding : MonoBehaviour
{
    public static bool IsOnboarded { get; private set; } = false;

    [CoolHeader("ONBOARDING!")]
    [Space]
    [Header("UI References")]
    public TMP_InputField UsernameInput;
    public TMP_Text       ClassDropDown;
    public GameObject     onboarding;
    public TMP_Text       ErrorText;
    public TMP_Text       uid;
    public GameObject     home;
    public WelcomeDrawSeq welcomeDrawSeq;

    [Header("Script References")]
    public SignUp signUpScript;

    void Awake()
    {
        IsOnboarded = PlayerPrefs.GetInt("OnboardingComplete", 0) == 1;
    }

    void Start()
    {
        if (signUpScript == null) signUpScript = FindObjectOfType<SignUp>();
        if (ErrorText    != null) ErrorText.gameObject.SetActive(false);
        if (uid          != null) uid.text = "UID: " + PlayerPrefs.GetString("SavedUserId", "");

        if (IsOnboarded)
        {
            if (onboarding != null) onboarding.SetActive(false);
            if (home       != null) home.SetActive(true);
            UserDataService.Instance?.FetchUserData();
        }
    }

    public bool CheckUser() => PlayerPrefs.GetInt("AnonUserLoggedIn", 0) == 1;

    public void CompleteOnboarding()
    {
        if (ErrorText != null) ErrorText.gameObject.SetActive(false);

        string username  = UsernameInput?.text.Trim() ?? "";
        string userClass = ClassDropDown?.text.Trim() ?? "";

        if (string.IsNullOrEmpty(username))
            { ShowError("Please enter a username!"); return; }
        if (string.IsNullOrEmpty(userClass) || userClass == "Select Class")
            { ShowError("Please select a class!"); return; }

        // Mark onboarded locally
        IsOnboarded = true;
        PlayerPrefs.SetInt("OnboardingComplete", 1);
        PlayerPrefs.Save();

        // Service handles PlayerPrefs + Firebase (if registered)
        UserDataService.Instance?.SaveProfile(username, userClass);
        UserDataService.Instance?.MarkOnboardingComplete();

        // Swap screens immediately
        if (onboarding != null) onboarding.SetActive(false);
        if (home       != null) home.SetActive(true);
        welcomeDrawSeq?.WelcomeDraw();
    }

    private void ShowError(string msg)
    {
        if (ErrorText == null) return;
        ErrorText.text = msg;
        ErrorText.gameObject.SetActive(true);
    }
}