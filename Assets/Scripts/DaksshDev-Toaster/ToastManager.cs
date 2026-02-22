using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DaksshDev.Toaster
{
    /// <summary>
    /// Optional per-call overrides. Any field left at its default is ignored
    /// and the value from ToastSettings is used instead.
    /// </summary>
    public struct ToastConfig
    {
        public Sprite customIcon;
        public Color? iconColor;
        public Color? textColor;
        public float  duration;

        public ToastConfig WithIcon(Sprite icon)      { customIcon = icon;  return this; }
        public ToastConfig WithIconColor(Color color) { iconColor  = color; return this; }
        public ToastConfig WithTextColor(Color color) { textColor  = color; return this; }
        public ToastConfig WithDuration(float secs)   { duration   = secs;  return this; }

        public static ToastConfig Default => new ToastConfig { duration = -1f };
    }

    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        [ToastHeader("Toast-Manager")]

        [Space]
        [Header("References")]
        [SerializeField] private Toast         toastPrefab;
        [SerializeField] private Transform     toastContainer;
        [SerializeField] private ToastSettings settings;

        [Header("Config")]
        [SerializeField] private float defaultDuration  = 3f;
        [SerializeField] private int   maxVisible       = 3;

        [Header("Stack Look")]
        [Tooltip("How many pixels each older toast peeks out from behind the front one")]
        [SerializeField] private float peekOffset       = 6f;
        [Tooltip("How much smaller each step in the stack gets (0.04 = 4% per step)")]
        [SerializeField] private float scaleStepDown    = 0.04f;
        [Tooltip("How much darker/transparent each step behind gets")]
        [SerializeField] private float alphaStepDown    = 0.12f;

        // index 0 = newest (front), last index = oldest (back)
        private readonly List<Toast> _stack = new List<Toast>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Show(string message, ToastType type, ToastConfig config)
        {
            if (toastPrefab == null || toastContainer == null || settings == null)
            {
                Debug.LogWarning("[ToastManager] Missing references. Cannot show toast.");
                return;
            }

            // Dismiss the oldest if we hit the cap
            if (_stack.Count >= maxVisible)
                _stack[_stack.Count - 1].Dismiss();

            float usedDuration = config.duration < 0f ? defaultDuration : config.duration;

            Toast toast = Instantiate(toastPrefab, toastContainer);
            _stack.Insert(0, toast);

            // Initialise AFTER inserting so GetStackIndex works immediately
            toast.Initialize(message, type, usedDuration, settings, config, this);

            StartCoroutine(UpdateStack());
        }

        public void Show(string message, ToastType type, float duration = -1f)
            => Show(message, type, ToastConfig.Default.WithDuration(duration));

        public void ShowSuccess(string message, float duration = -1f) => Show(message, ToastType.Success, duration);
        public void ShowError  (string message, float duration = -1f) => Show(message, ToastType.Error,   duration);
        public void ShowWarning(string message, float duration = -1f) => Show(message, ToastType.Warning, duration);
        public void ShowNotify (string message, float duration = -1f) => Show(message, ToastType.Notify,  duration);

        public void ShowSuccess(string message, ToastConfig config) => Show(message, ToastType.Success, config);
        public void ShowError  (string message, ToastConfig config) => Show(message, ToastType.Error,   config);
        public void ShowWarning(string message, ToastConfig config) => Show(message, ToastType.Warning, config);
        public void ShowNotify (string message, ToastConfig config) => Show(message, ToastType.Notify,  config);

        // ── Called by Toast on dismiss ─────────────────────────────────────────

        public void OnToastDismissed(Toast toast)
        {
            _stack.Remove(toast);
            StartCoroutine(UpdateStack());
        }

        // ── Stack layout ───────────────────────────────────────────────────────

        /// <summary>
        /// Recalculates every toast's target position, scale, and alpha
        /// and asks each one to animate there.
        /// index 0 = front/top, full size, full alpha, Y = 0
        /// index 1 = one step back, slightly smaller, slightly offset downward
        /// index 2 = two steps back, etc.
        /// </summary>
        public IEnumerator UpdateStack()
        {
            // One frame so newly instantiated toasts have a measured rect
            yield return null;

            for (int i = 0; i < _stack.Count; i++)
            {
                Toast t = _stack[i];
                if (t == null) continue;

                // The front toast (i=0) sits at Y=0 with scale 1 and alpha 1.
                // Each step behind: offset downward, shrink, fade.
                float targetY     =  i * peekOffset;           // positive = down in a top-anchored container
                float targetScale =  1f - (i * scaleStepDown);
                float targetAlpha =  1f - (i * alphaStepDown);
                int   siblingIdx  = _stack.Count - 1 - i;      // front toast is last sibling = drawn on top

                t.transform.SetSiblingIndex(siblingIdx);
                t.AnimateTo(targetY, targetScale, Mathf.Clamp01(targetAlpha), i == 0);
            }
        }
    }
}