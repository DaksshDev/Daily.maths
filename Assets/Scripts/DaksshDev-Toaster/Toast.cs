using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DaksshDev.Toaster
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(ToastAnimator))]
    public class Toast : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [ToastHeader("TOAST")]

        [Space]
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image           iconImage;
        [SerializeField] private Slider          progressSlider;
        [SerializeField] private Button          closeButton;

        private const float StackAnimDuration = 0.30f;

        private ToastAnimator _animator;
        private CanvasGroup   _canvasGroup;
        private RectTransform _rectTransform;
        private ToastManager  _manager;

        private float _duration;
        private float _elapsed;
        private bool  _dismissing;

        // Cached so swipe direction can match exit anim
        private ExitAnimation _exitAnimation;

        // Running stack-reposition coroutine — interrupted on each new call
        private Coroutine _stackAnimCoroutine;

        // Swipe
        private Vector2 _dragStartPos;
        private Vector2 _initialAnchoredPos;
        private const float SwipeDismissThreshold = 80f;

        private void Awake()
        {
            _animator      = GetComponent<ToastAnimator>();
            _canvasGroup   = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        // ── Init ──────────────────────────────────────────────────────────────

        public void Initialize(string message, ToastType type, float duration,
                               ToastSettings settings, ToastConfig config,
                               ToastManager manager)
        {
            _duration      = duration;
            _manager       = manager;
            _exitAnimation = settings.exitAnimation; // cache for swipe

            messageText.text = message;

            if (config.textColor.HasValue)
                messageText.color = config.textColor.Value;
            else if (settings.coloredText)
                messageText.color = settings.GetTextColor(type);

            iconImage.sprite = config.customIcon != null ? config.customIcon : settings.GetIcon(type);
            iconImage.color  = config.iconColor.HasValue  ? config.iconColor.Value : settings.GetIconColor(type);

            if (closeButton != null)
                closeButton.onClick.AddListener(Dismiss);

            if (progressSlider != null)
            {
                progressSlider.minValue     = 0f;
                progressSlider.maxValue     = 1f;
                progressSlider.value        = 1f;
                progressSlider.interactable = false;

                Image fillImage = progressSlider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                    fillImage.color = config.iconColor.HasValue
                        ? config.iconColor.Value
                        : settings.GetIconColor(type);
            }

            _animator.SetExitAnimation(settings.exitAnimation);
            _animator.PlayEnter(settings.enterAnimation, OnEnterComplete);
        }

        private void OnEnterComplete() => StartCoroutine(LifecycleRoutine());

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator LifecycleRoutine()
        {
            _elapsed = 0f;
            while (_elapsed < _duration && !_dismissing)
            {
                _elapsed += Time.deltaTime;
                if (progressSlider != null)
                    progressSlider.value = 1f - Mathf.Clamp01(_elapsed / _duration);
                yield return null;
            }

            if (!_dismissing) Dismiss();
        }

        public void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            StopAllCoroutines();
            _animator.PlayExit(OnExitComplete);
        }

        private void OnExitComplete()
        {
            _manager?.OnToastDismissed(this);
            Destroy(gameObject);
        }

        // ── Stack animation ───────────────────────────────────────────────────

        public void AnimateTo(float targetY, float targetScale, float targetAlpha, bool isFront)
        {
            if (_stackAnimCoroutine != null)
                StopCoroutine(_stackAnimCoroutine);

            _stackAnimCoroutine = StartCoroutine(
                AnimateToRoutine(targetY, targetScale, targetAlpha, isFront)
            );
        }

        private IEnumerator AnimateToRoutine(float targetY, float targetScale,
                                             float targetAlpha, bool isFront)
        {
            float startY     = _rectTransform.anchoredPosition.y;
            float startScale = _rectTransform.localScale.x;
            float startAlpha = _canvasGroup.alpha;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / StackAnimDuration;
                float e = EaseOutCubic(t);

                Vector2 pos = _rectTransform.anchoredPosition;
                pos.y = Mathf.Lerp(startY, targetY, e);
                _rectTransform.anchoredPosition = pos;

                float s = Mathf.Lerp(startScale, targetScale, e);
                _rectTransform.localScale = new Vector3(s, s, 1f);

                if (!isFront)
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, e);

                yield return null;
            }

            Vector2 finalPos = _rectTransform.anchoredPosition;
            finalPos.y = targetY;
            _rectTransform.anchoredPosition = finalPos;
            _rectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
            if (!isFront) _canvasGroup.alpha = targetAlpha;

            _stackAnimCoroutine = null;
        }

        public float GetHeight() => _rectTransform.rect.height > 0f
            ? _rectTransform.rect.height
            : 80f;

        private static float EaseOutCubic(float t) =>
            1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        // ── Swipe to dismiss ──────────────────────────────────────────────────

        // Returns the drag delta axis and sign that matches the exit animation.
        // x > 0 → swipe right, x < 0 → swipe left, y < 0 → swipe down, etc.
        private Vector2 GetSwipeDelta(Vector2 dragDelta)
        {
            switch (_exitAnimation)
            {
                case ExitAnimation.SlideToLeft:  return new Vector2(Mathf.Min(dragDelta.x, 0f), 0f); // only left counts
                case ExitAnimation.SlideToRight: return new Vector2(Mathf.Max(dragDelta.x, 0f), 0f); // only right counts
                case ExitAnimation.FadeOut:
                case ExitAnimation.PopOut:
                default:
                    return dragDelta; // any direction works for non-directional exits
            }
        }

        // How far the user has dragged in the meaningful direction (0–1)
        private float GetSwipeProgress(Vector2 dragDelta)
        {
            switch (_exitAnimation)
            {
                case ExitAnimation.SlideToLeft:  return Mathf.Clamp01(-dragDelta.x / SwipeDismissThreshold);
                case ExitAnimation.SlideToRight: return Mathf.Clamp01( dragDelta.x / SwipeDismissThreshold);
                case ExitAnimation.FadeOut:
                case ExitAnimation.PopOut:
                default:
                    return Mathf.Clamp01(dragDelta.magnitude / SwipeDismissThreshold);
            }
        }

        private bool ShouldDismissOnRelease(Vector2 dragDelta)
        {
            return GetSwipeProgress(dragDelta) >= 1f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPos       = eventData.position;
            _initialAnchoredPos = _rectTransform.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 rawDelta    = (Vector2)eventData.position - _dragStartPos;
            Vector2 swipeDelta  = GetSwipeDelta(rawDelta);
            float   progress    = GetSwipeProgress(rawDelta);

            _rectTransform.anchoredPosition = _initialAnchoredPos + swipeDelta;
            _canvasGroup.alpha = 1f - (progress * 0.4f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 rawDelta = (Vector2)eventData.position - _dragStartPos;

            if (ShouldDismissOnRelease(rawDelta))
            {
                Dismiss();
            }
            else
            {
                _rectTransform.anchoredPosition = _initialAnchoredPos;
                _canvasGroup.alpha = 1f;
            }
        }
    }
}