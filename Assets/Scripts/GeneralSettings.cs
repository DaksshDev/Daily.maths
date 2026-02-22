using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DaksshDev.Toaster;

public class GeneralSettings : MonoBehaviour
{
    [SettingsHeader("General Settings (i.e- sound, reset, haptics)")]

    [Space]
    [Header("Volume")]
    public Slider   volumeSlider;
    public TMP_Text volumePercentText;

    [Header("Haptic Feedback")]
    public TMP_Text hapticButtonText;

    private const string VolumeKey = "Settings_Volume";
    private const string HapticKey = "Settings_Haptic";

    private bool hapticEnabled;

    // =========================================================================
    //  Unity Lifecycle
    // =========================================================================

    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        hapticEnabled     = PlayerPrefs.GetInt(HapticKey, 0) == 1;

        AudioListener.volume = savedVolume;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value    = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        RefreshVolumeLabel(savedVolume);
        RefreshHapticLabel();
    }

    // =========================================================================
    //  Volume
    // =========================================================================

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
        RefreshVolumeLabel(value);
    }

    private void RefreshVolumeLabel(float value)
    {
        if (volumePercentText != null)
            volumePercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    // =========================================================================
    //  Haptic
    // =========================================================================

    public void ToggleHaptic()
    {
        hapticEnabled = !hapticEnabled;
        PlayerPrefs.SetInt(HapticKey, hapticEnabled ? 1 : 0);
        PlayerPrefs.Save();
        RefreshHapticLabel();
    }

    private void RefreshHapticLabel()
    {
        if (hapticButtonText == null) return;
        hapticButtonText.text = hapticEnabled
            ? "Haptic Feedback : <color=green>ON</color>"
            : "Haptic Feedback : <color=grey>OFF</color>";
    }

    // =========================================================================
    //  Reset
    // =========================================================================

    /// <summary>
    /// Wipes everything locally. Keeps Firebase account intact.
    /// Use for a soft "start over" without deleting the account.
    /// </summary>
    public void FactoryReset()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Full nuclear option — deletes Firebase Auth + Firestore doc + PlayerPrefs,
    /// then reloads the scene. Shows toast feedback during the process.
    /// </summary>
    public void ResetAndDeleteAccount()
    {
        ToastManager.Instance?.ShowNotify("Deleting account...");

        UserDataService.Instance?.ResetAndDeleteAccount(
            onComplete: () =>
            {
                ToastManager.Instance?.ShowSuccess("Account deleted. See you around!");
                Invoke(nameof(ReloadScene), 1.5f);
            },
            onError: err =>
            {
                ToastManager.Instance?.ShowError("Delete failed: " + err);
            }
        );
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // =========================================================================
    //  Public Helpers
    // =========================================================================

    public static bool IsHapticEnabled() => PlayerPrefs.GetInt(HapticKey, 0) == 1;
    public static float GetVolume()      => PlayerPrefs.GetFloat(VolumeKey, 1f);
}