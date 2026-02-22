using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Progression : MonoBehaviour
{
    [CoolHeader("ProgressDisplay(HomeScreen)")]

    [Space]
    [Header("References")]
    public HomeScreenMgr homeScreenMgr;
    public TMP_Text levelText;
    public TMP_Text progressText;
    public Slider progressSlider;
    public TMP_Text percentText;

    [Header("Settings")]
    public bool fetchOnStart = true;

    private int   currentLevel    = 0;
    private int   coinsToNext     = 0;
    private int   xpToNext        = 0;
    private float progressPercent = 0f;

    private Color startColor   = new Color(0.58f, 0f, 1f, 1f);
    private Color endColor     = new Color(1f, 0.38f, 0f, 1f);
    private Color defaultColor = new Color32(47, 47, 47, 255);

    void Start()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value    = 0f;
        }
        CalculateLevel();
    }

    public void CalculateLevel()
    {
        if (!Onboarding.IsOnboarded)
        {
            Debug.LogWarning("CalculateLevel called but onboarding not complete. Skipping.");
            return;
        }

        if (homeScreenMgr == null)
        {
            Debug.LogError("HomeScreenMgr reference is missing!");
            return;
        }

        int totalProgress = homeScreenMgr.GetCoins() + homeScreenMgr.GetXP();

        int level         = 0;
        int cumulativeSum = 0;

        while (cumulativeSum + GetLevelRequirement(level) <= totalProgress)
        {
            cumulativeSum += GetLevelRequirement(level);
            level++;
        }

        currentLevel = level;

        int nextLevelRequirement = GetLevelRequirement(level);
        int remainingProgress    = totalProgress - cumulativeSum;
        int progressNeeded       = nextLevelRequirement - remainingProgress;

        coinsToNext     = (progressNeeded * 2) / 3;
        xpToNext        = progressNeeded - coinsToNext;
        progressPercent = (float)remainingProgress / nextLevelRequirement;

        PlayerPrefs.SetInt("currentLevel", currentLevel);
        PlayerPrefs.Save();

        UserDataService.Instance?.SaveLevel(currentLevel);

        UpdateUI();
    }

    private int GetLevelRequirement(int level) => 1500 + (level * 500);

    private void UpdateUI()
    {
        if (levelText != null)
        {
            levelText.text = "Level " + currentLevel;

            if (currentLevel >= 50)
                levelText.color = endColor;
            else if (currentLevel >= 5)
                levelText.color = Color.Lerp(startColor, endColor, (currentLevel - 5) / 45f);
            else
                levelText.color = defaultColor;
        }

        if (progressText != null)
            progressText.text = coinsToNext + " coins, " + xpToNext + " xp to next level";

        if (progressSlider != null)
            progressSlider.value = progressPercent;

        if (percentText != null)
            percentText.text = Mathf.RoundToInt(progressPercent * 100f) + "%";
    }

    public void OnProgressChanged() => CalculateLevel();

    public int   GetCurrentLevel()    => currentLevel;
    public int   GetCoinsToNext()     => coinsToNext;
    public int   GetXPToNext()        => xpToNext;
    public float GetProgressPercent() => progressPercent;
}