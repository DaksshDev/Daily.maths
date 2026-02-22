using System.Collections;
using UnityEngine;

public class VoltixCamShaker : MonoBehaviour
{
    [SettingsHeader("imvoltix's shaking machine :skull:")]
    [Header("Shake Settings")]
    [SerializeField] private float defaultIntensity = 10f;
    [SerializeField] private float defaultDuration = 0.3f;
    [SerializeField] private float comboIntensity = 20f;
    [SerializeField] private float comboDuration = 0.5f;

    [Header("Target")]
    [SerializeField] private Transform shakeTarget;

   // [Header("Debug")]
   // [SerializeField] private bool testShake = false;

    private bool isShaking = false;
    private Vector3 originalPosition;

    void Start()
    {
        if (shakeTarget == null)
            shakeTarget = transform;

        RectTransform rectTransform = shakeTarget.GetComponent<RectTransform>();
        originalPosition = rectTransform != null
            ? (Vector3)rectTransform.anchoredPosition
            : shakeTarget.localPosition;
    }

   // void Update()
   // {
   //     if (testShake && !isShaking)
   //     {
   //        testShake = false;
   //        ShakeNormal();
   //   }
   //}

    public void ShakeNormal() => StartCoroutine(Shake(defaultIntensity, defaultDuration));
    public void ShakeCombo() => StartCoroutine(Shake(comboIntensity, comboDuration));
    public void ShakeCustom(float intensity, float duration) => StartCoroutine(Shake(intensity, duration));

    public void StopShake()
    {
        StopAllCoroutines();
        ResetPosition();
        isShaking = false;
    }

    private IEnumerator Shake(float intensity, float duration)
    {
        if (shakeTarget == null || isShaking) yield break;

        isShaking = true;
        float elapsed = 0f;

        RectTransform rectTransform = shakeTarget.GetComponent<RectTransform>();
        bool isUIElement = rectTransform != null;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;

            if (isUIElement)
                rectTransform.anchoredPosition = (Vector2)originalPosition + new Vector2(x, y);
            else
                shakeTarget.localPosition = originalPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetPosition();
        isShaking = false;
    }

    private void ResetPosition()
    {
        if (shakeTarget == null) return;

        RectTransform rectTransform = shakeTarget.GetComponent<RectTransform>();

        if (rectTransform != null)
            rectTransform.anchoredPosition = originalPosition;
        else
            shakeTarget.localPosition = originalPosition;
    }

    public bool IsShaking() => isShaking;
}