using UnityEngine;
using System.Collections;

public class PopIn : MonoBehaviour
{
    [SettingsHeader("Pop-In Effect")]
    
    [Space]
    public float duration = 0.3f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    void OnEnable()
    {
        StartCoroutine(DoPopIn());
    }

    IEnumerator DoPopIn()
    {
        float elapsed = 0f;
        transform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(elapsed / duration);
            transform.localScale = Vector3.one * t;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}