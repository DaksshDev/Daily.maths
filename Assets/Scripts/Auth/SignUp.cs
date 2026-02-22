using UnityEngine;
using Firebase.Auth;
using System.Collections;
using Firebase.Extensions;
using TMPro;

/// <summary>
/// Handles anonymous Firebase auth UI only.
/// All data ops go through UserDataService.
/// </summary>
public class SignUp : MonoBehaviour
{
    FirebaseAuth auth;

    [CoolHeader("Signup (Auth Manager)")]

    [Space]
    [Header("Auth Modal")]
    public GameObject FreshStart;
    public GameObject FsDrawer;
    public TMP_Text   SignUpButtonErrorIndicator;

    [Header("References")]
    public WelcomeDrawSeq welcomeDrawSeq;
    public GameObject     OnboardingScreen;
    public GameObject     loading;

    private Drawer drawer;

    private const string ANON_USER_KEY = "AnonUserLoggedIn";
    private const string USER_ID_KEY   = "SavedUserId";

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;

        if (FsDrawer != null)
            drawer = FsDrawer.GetComponent<Drawer>();

        if (FreshStart != null)
            FreshStart.SetActive(false);
    }

    // ==========================================================================
    //  Public
    // ==========================================================================

    public bool   IsRegistered()   => PlayerPrefs.GetInt(ANON_USER_KEY, 0) == 1;
    public string GetSavedUserId() => PlayerPrefs.GetString(USER_ID_KEY, "");

    /// <summary>
    /// Guest path — no Firebase needed.
    /// Goes straight to onboarding so user picks username/class locally.
    /// </summary>
    public void ContinueAsGuest()
    {
        if (Onboarding.IsOnboarded)
        {
            HideAuthModal();
            UserDataService.Instance?.FetchUserData();
            return;
        }

        HideAuthModal();
        if (OnboardingScreen != null)
            OnboardingScreen.SetActive(true);
    }

    public void ShowAuthModal()
    {
        if (FreshStart != null) FreshStart.SetActive(true);
    }

    public void HideAuthModal()
    {
        if (FreshStart != null) FreshStart.SetActive(false);
    }

    /// <summary>
    /// Cloud sign-up. Creates Firebase anon account then tells
    /// UserDataService to upload all local guest data to Firestore.
    /// </summary>
    public void AuthNewAccount()
    {
        if (IsRegistered())
        {
            Debug.Log("Already registered — skipping auth.");
            StartCoroutine(LoadAndContinue());
            return;
        }

        if (!(UserDataService.Instance?.IsOnline ?? false))
        {
            SetButtonError("No internet connection.");
            return;
        }

        StartCoroutine(welcomeDrawSeq.SetupDrawerInClosedPosition());
        auth.SignOut();

        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Anonymous sign-in failed: " + task.Exception);
                SetButtonError("Sign in failed. Try again.");
                return;
            }

            FirebaseUser newUser = task.Result.User;
            Debug.Log("✅ Signed in anonymously: " + newUser.UserId);

            // Persist auth state locally
            PlayerPrefs.SetInt   (ANON_USER_KEY, 1);
            PlayerPrefs.SetString(USER_ID_KEY,   newUser.UserId);
            PlayerPrefs.Save();

            // Hand off to service — it transitions Guest → Authenticated
            // and creates the Firestore doc with all local guest data
            UserDataService.Instance?.RegisterNewAccount(newUser.UserId);

            StartCoroutine(LoadAndContinue());
        });
    }

    public void SignOut()
    {
        PlayerPrefs.DeleteKey(ANON_USER_KEY);
        PlayerPrefs.DeleteKey(USER_ID_KEY);
        PlayerPrefs.Save();

        if (auth?.CurrentUser != null) auth.SignOut();
        Debug.Log("🗑️ Signed out — back to Guest");
    }

    // ==========================================================================
    //  Internal
    // ==========================================================================

    private IEnumerator LoadAndContinue()
    {
        if (loading != null) loading.SetActive(true);
        CloseDrawer();
        yield return new WaitForSeconds(1f);
        HideAuthModal();
        yield return new WaitForSeconds(2f);
        if (loading != null) loading.SetActive(false);

        if (Onboarding.IsOnboarded)
            UserDataService.Instance?.FetchUserData();
        else if (OnboardingScreen != null)
            OnboardingScreen.SetActive(true);
    }

    private void CloseDrawer()
    {
        if (drawer != null) drawer.SetDrawerPosition(-1300);
    }

    private void SetButtonError(string msg)
    {
        if (SignUpButtonErrorIndicator != null)
            SignUpButtonErrorIndicator.text = msg;
        StartCoroutine(ClearButton());
    }

    private IEnumerator ClearButton()
    {
        yield return new WaitForSeconds(3f);
        if (SignUpButtonErrorIndicator != null)
            SignUpButtonErrorIndicator.text = "Get Started";
    }
}