using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Splash screen. Guests boot straight to game — no auth check, no blocking.
/// </summary>
public class loadingSplash : MonoBehaviour
{
    [System.Serializable]
    public class LoadingStage
    {
        [Range(0f, 100f)]
        public float  targetPercentage = 25f;
        public float  duration         = 2f;
        public string statusText       = "Loading...";
    }

    [CoolHeader("Splash Loading Manager")]

    [Space]
    [Header("UI References")]
    [SerializeField] private Slider   progressSlider;
    [SerializeField] private TMP_Text progressText;

    [Header("Loading Stages")]
    [SerializeField] private List<LoadingStage> loadingStages = new List<LoadingStage>
    {
        new LoadingStage { targetPercentage = 40f,  duration = 1.5f, statusText = "Initialising..." },
        new LoadingStage { targetPercentage = 80f,  duration = 1.5f, statusText = "Loading game..."  },
        new LoadingStage { targetPercentage = 100f, duration = 0.5f, statusText = "Let's go!"        }
    };

    [Header("Screen References")]
    [SerializeField] private GameObject loadingScreenGameObject;
    [SerializeField] private GameObject onboardingScreen;

    [Header("Fresh Install")]
    [Tooltip("Panel shown on very first launch — pick Guest or Sign Up")]
    [SerializeField] private GameObject freshInstallPanel;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Home Drawer")]
    public GameObject     HomeDrawer;
    public WelcomeDrawSeq welcomeDrawSeq;

    private float       currentProgress = 0f;
    private CanvasGroup canvasGroup;

    // ── PlayerPrefs keys we consider "complete" for a returning user ──────────
    // All three must exist and be non-empty/non-zero for us to trust the save.
    private bool IsSaveComplete()
    {
        bool hasOnboarded = PlayerPrefs.GetInt   ("OnboardingComplete", 0) == 1;
        bool hasUsername  = !string.IsNullOrEmpty(PlayerPrefs.GetString("username", ""));
        bool hasClass     = !string.IsNullOrEmpty(PlayerPrefs.GetString("class",    ""));
        return hasOnboarded && hasUsername && hasClass;
    }

    // ==========================================================================

    void Start()
    {
        if (loadingScreenGameObject != null)
            canvasGroup = loadingScreenGameObject.GetComponent<CanvasGroup>();

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 100f;
            progressSlider.value    = 0f;
        }

        // Hide fresh install panel at boot — we decide later
        if (freshInstallPanel != null)
            freshInstallPanel.SetActive(false);

        loadingStages.Sort((a, b) => a.targetPercentage.CompareTo(b.targetPercentage));
        StartCoroutine(LoadingSequence());
    }

    // ==========================================================================

    private IEnumerator LoadingSequence()
    {
        float previousPercentage = 0f;

        foreach (LoadingStage stage in loadingStages)
        {
            if (progressText != null) progressText.text = stage.statusText;

            float elapsed = 0f;
            while (elapsed < stage.duration)
            {
                elapsed         += Time.deltaTime;
                currentProgress  = Mathf.Lerp(previousPercentage, stage.targetPercentage, elapsed / stage.duration);
                if (progressSlider != null) progressSlider.value = currentProgress;
                yield return null;
            }

            currentProgress = stage.targetPercentage;
            if (progressSlider != null) progressSlider.value = currentProgress;
            previousPercentage = stage.targetPercentage;
        }

        yield return StartCoroutine(OnLoadingComplete());
    }

    private IEnumerator OnLoadingComplete()
    {
        if (!IsSaveComplete())
        {
            // ── Incomplete / fresh install ────────────────────────────────────
            // Could be: brand new user, corrupted prefs, or partial onboarding.
            // Show the fresh install panel so they can choose Guest or Sign Up.
            if (welcomeDrawSeq != null)
                StartCoroutine(welcomeDrawSeq.SetupDrawerInClosedPosition());

            // If mid-onboarding (has account but never finished), go back to onboarding
            bool hasAccount = PlayerPrefs.GetInt("AnonUserLoggedIn", 0) == 1;
            if (hasAccount && onboardingScreen != null)
            {
                // They started sign-up but never finished onboarding — resume it
                onboardingScreen.SetActive(true);
            }
            else
            {
                // True fresh install — show the welcome/choice panel
                if (freshInstallPanel != null)
                    freshInstallPanel.SetActive(true);
            }
        }
        else
        {
            // ── Returning user with complete save ─────────────────────────────
            if (HomeDrawer != null) HomeDrawer.SetActive(true);
            yield return StartCoroutine(FadeOutAndHide());
            UserDataService.Instance?.FetchUserData();
        }
    }

    private IEnumerator FadeOutAndHide()
    {
        if (canvasGroup != null)
        {
            float elapsed    = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed           += Time.deltaTime;
                canvasGroup.alpha  = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }
        else
        {
            yield return new WaitForSeconds(fadeOutDuration);
        }

        if (loadingScreenGameObject != null)
            loadingScreenGameObject.SetActive(false);
    }

    // ==========================================================================

    public float GetTotalLoadingTime()
    {
        float total = 0f;
        foreach (var s in loadingStages) total += s.duration;
        return total;
    }
}