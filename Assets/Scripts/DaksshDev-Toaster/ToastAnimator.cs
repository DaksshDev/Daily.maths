using System;
using System.Collections;
using UnityEngine;

namespace DaksshDev.Toaster
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastAnimator : MonoBehaviour
    {
        [ToastHeader("Toast-Animator")]
        
        [Space]
        private const float AnimDuration = 0.35f;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private ExitAnimation _exitAnimation;

        private void Awake()
        {
            _canvasGroup   = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        // Called by Toast.Initialize – caches exit type and runs enter
        public void PlayEnter(EnterAnimation type, Action onComplete)
        {
            StartCoroutine(EnterRoutine(type, onComplete));
        }

        // Called by Toast.Dismiss – uses cached exit type
        public void PlayExit(Action onComplete)
        {
            StartCoroutine(ExitRoutine(_exitAnimation, onComplete));
        }

        // Allows Toast to supply exit animation from settings before calling PlayExit
        public void SetExitAnimation(ExitAnimation type)
        {
            _exitAnimation = type;
        }

        // ── Enter routines ────────────────────────────────────────────────────

        private IEnumerator EnterRoutine(EnterAnimation type, Action onComplete)
        {
            float width = _rectTransform.rect.width;
            if (width == 0f) width = 400f; // fallback before layout pass

            _canvasGroup.alpha = 0f;

            switch (type)
            {
                case EnterAnimation.SlideFromRight:
                    yield return SlideEnter(new Vector2(width + 50f, 0f), Vector2.zero);
                    break;

                case EnterAnimation.SlideFromLeft:
                    yield return SlideEnter(new Vector2(-(width + 50f), 0f), Vector2.zero);
                    break;

                case EnterAnimation.FadeIn:
                    yield return Fade(0f, 1f);
                    break;

                case EnterAnimation.PopIn:
                    yield return PopEnter();
                    break;
            }

            onComplete?.Invoke();
        }

        private IEnumerator ExitRoutine(ExitAnimation type, Action onComplete)
        {
            float width = _rectTransform.rect.width;
            if (width == 0f) width = 400f;

            switch (type)
            {
                case ExitAnimation.SlideToLeft:
                    yield return SlideExit(new Vector2(-(width + 50f), 0f));
                    break;

                case ExitAnimation.SlideToRight:
                    yield return SlideExit(new Vector2(width + 50f, 0f));
                    break;

                case ExitAnimation.FadeOut:
                    yield return Fade(1f, 0f);
                    break;

                case ExitAnimation.PopOut:
                    yield return PopExit();
                    break;
            }

            onComplete?.Invoke();
        }

        // ── Animation helpers ─────────────────────────────────────────────────

        private IEnumerator SlideEnter(Vector2 from, Vector2 to)
        {
            Vector2 origin = _rectTransform.anchoredPosition;
            _rectTransform.anchoredPosition = origin + from;
            _canvasGroup.alpha = 1f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / AnimDuration;
                _rectTransform.anchoredPosition = Vector2.Lerp(origin + from, origin + to, EaseOut(t));
                yield return null;
            }
            _rectTransform.anchoredPosition = origin + to;
        }

        private IEnumerator SlideExit(Vector2 delta)
        {
            Vector2 origin = _rectTransform.anchoredPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / AnimDuration;
                _rectTransform.anchoredPosition = Vector2.Lerp(origin, origin + delta, EaseIn(t));
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / AnimDuration;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        private IEnumerator PopEnter()
        {
            _canvasGroup.alpha = 1f;
            _rectTransform.localScale = Vector3.zero;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / AnimDuration;
                float s = PopOvershoot(t);
                _rectTransform.localScale = Vector3.one * s;
                yield return null;
            }
            _rectTransform.localScale = Vector3.one;
        }

        private IEnumerator PopExit()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / AnimDuration;
                float s = Mathf.Lerp(1f, 0f, EaseIn(t));
                _rectTransform.localScale = Vector3.one * s;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }
        }

        // ── Easing ────────────────────────────────────────────────────────────

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float EaseIn(float t)  => Mathf.Pow(Mathf.Clamp01(t), 2f);

        // Simple spring overshoot for iOS-style pop
        private static float PopOvershoot(float t)
        {
            t = Mathf.Clamp01(t);
            // Approximate spring: goes slightly above 1 then settles
            return Mathf.Sin(t * Mathf.PI * (0.2f + 2.5f * t * t * t)) * Mathf.Pow(1f - t, 2.2f) + t;
        }
    }
}