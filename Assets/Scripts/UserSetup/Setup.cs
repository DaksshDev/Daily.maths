using UnityEngine;
using System.Collections;
using TMPro;

public class Setup : MonoBehaviour
{
    [CoolHeader("Setup")]

    [Space]
    [Header("Auth Modal")]
    public GameObject FreshStart;
    public GameObject FsDrawer;
    public TMP_Text   SignUpButtonErrorIndicator;

    [Header("References")]
    public GameObject     OnboardingScreen;
    public GameObject     loading;

    private Drawer drawer;

    void Start()
    {
        if (FsDrawer  != null) drawer = FsDrawer.GetComponent<Drawer>();
        if (FreshStart != null) FreshStart.SetActive(false);
    }

    // ==========================================================================
    //  Public
    // ==========================================================================

    /// <summary>
    /// Goes to onboarding if not yet onboarded, otherwise straight to game.
    /// </summary>
    public void Continue()
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

    // ==========================================================================
    //  Internal
    // ==========================================================================

    private void CloseDrawer()
    {
        if (drawer != null) drawer.SetDrawerPosition(-1300);
    }
}