using UnityEngine;

public class HapticsMgr : MonoBehaviour
{
    [CoolHeader("Haptics Manager (Android-Only)")]
    
    [Space]
    private const string HapticKey = "Settings_Haptic";

    private bool hapticEnabled;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private bool              isAPI26Plus;
#endif

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator   = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            isAPI26Plus = version.GetStatic<int>("SDK_INT") >= 26;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[HapticsMgr] Failed to init vibrator: " + e.Message);
        }
#endif
    }

    void Start()
    {
        hapticEnabled = PlayerPrefs.GetInt(HapticKey, 0) == 1;
    }

    public void VibrateCorrect()  => Vibrate(80,  180);
    public void VibrateWrong()    => Vibrate(255, 120);
    public void VibrateSkipped()  => Vibrate(100,  60);
    public void VibrateDefault()  => Vibrate(128,  50);

    private void Vibrate(int amplitude, long milliseconds)
    {
        if (!hapticEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (vibrator == null) return;

            if (isAPI26Plus)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect      = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[HapticsMgr] Vibration failed: " + e.Message);
        }

#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        vibrator?.Dispose();
#endif
    }
}