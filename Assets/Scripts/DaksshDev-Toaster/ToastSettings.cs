using UnityEngine;

namespace DaksshDev.Toaster
{
    public enum EnterAnimation
    {
        SlideFromRight,
        SlideFromLeft,
        FadeIn,
        PopIn
    }

    public enum ExitAnimation
    {
        SlideToLeft,
        SlideToRight,
        FadeOut,
        PopOut
    }

    [CreateAssetMenu(fileName = "ToastSettings", menuName = "DaksshDev/Toaster/Toast Settings", order = 0)]
    public class ToastSettings : ScriptableObject
    {
        [ToastHeader("Toast-Configuration")]

        // ── Icons ──────────────────────────────────────────────────────────────
        [Space]
        [Header("Icons")]
        public Sprite successIcon;
        public Sprite errorIcon;
        public Sprite warningIcon;
        public Sprite notifyIcon;

        // ── Icon Tint Colors ───────────────────────────────────────────────────
        [Header("Icon Tint Colors")]
        public Color successIconColor = new Color(0.18f, 0.80f, 0.44f, 1f); // green
        public Color errorIconColor   = new Color(0.91f, 0.30f, 0.24f, 1f); // red
        public Color warningIconColor = new Color(0.95f, 0.77f, 0.06f, 1f); // amber
        public Color notifyIconColor  = new Color(0.20f, 0.60f, 1.00f, 1f); // blue

        // ── Text ───────────────────────────────────────────────────────────────
        [Header("Text")]
        public bool coloredText = false;

        [Header("Text Colors (used when Colored Text is enabled)")]
        public Color successTextColor = new Color(0.18f, 0.80f, 0.44f, 1f);
        public Color errorTextColor   = new Color(0.91f, 0.30f, 0.24f, 1f);
        public Color warningTextColor = new Color(0.95f, 0.77f, 0.06f, 1f);
        public Color notifyTextColor  = new Color(0.20f, 0.60f, 1.00f, 1f);

        // ── Animations ─────────────────────────────────────────────────────────
        [Header("Animations")]
        public EnterAnimation enterAnimation = EnterAnimation.SlideFromRight;
        public ExitAnimation  exitAnimation  = ExitAnimation.SlideToLeft;

        // ── Helpers ────────────────────────────────────────────────────────────

        public Color GetIconColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => successIconColor,
                ToastType.Error   => errorIconColor,
                ToastType.Warning => warningIconColor,
                _                 => notifyIconColor,
            };
        }

        public Color GetTextColor(ToastType type)
        {
            return type switch
            {
                ToastType.Success => successTextColor,
                ToastType.Error   => errorTextColor,
                ToastType.Warning => warningTextColor,
                _                 => notifyTextColor,
            };
        }

        public Sprite GetIcon(ToastType type)
        {
            return type switch
            {
                ToastType.Success => successIcon,
                ToastType.Error   => errorIcon,
                ToastType.Warning => warningIcon,
                _                 => notifyIcon,
            };
        }
    }
}