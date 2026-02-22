using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class EarnAnim : MonoBehaviour
{
    [SettingsHeader("SICK Earn Animation")]
    
    [Space]
    [Header("=== IDENTIFIER ===")]
    [SerializeField] private string usageName = "xp/coins";

    [Header("References")]
    public GameObject    panelPrefab;
    public RectTransform spawnPoint;
    public RectTransform collectionPanel;
    public Canvas        rootCanvas;
    public AudioSource   audioSource;

    [Header("SFX")]
    public AudioClip appearClip;
    public AudioClip disappearClip;

    [Header("Settings")]
    public float spawnSpread   = 40f;
    public float spawnDelay    = 0.05f;
    public float holdDelay     = 0.15f;
    public float flyDuration   = 0.25f;
    public float punchScale    = 1.3f;
    public float punchDuration = 0.2f;
    public int   maxPrefabs    = 12;

    public void Play(int count, Action onComplete = null)
    {
        if (panelPrefab == null || spawnPoint == null || collectionPanel == null)
        {
            Debug.LogError($"[EarnAnim:{usageName}] Missing references!");
            onComplete?.Invoke();
            return;
        }

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        StartCoroutine(SpawnAndFly(Mathf.Clamp(count, 1, maxPrefabs), onComplete));
    }

    IEnumerator SpawnAndFly(int count, Action onComplete)
    {
        RectTransform[] icons = new RectTransform[count];

        for (int i = 0; i < count; i++)
        {
            if (i == 0) PlaySound(appearClip);

            GameObject obj   = Instantiate(panelPrefab, rootCanvas.transform);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.position      = spawnPoint.position + new Vector3(
                Random.Range(-spawnSpread, spawnSpread),
                Random.Range(-spawnSpread, spawnSpread), 0f);

            icons[i] = rt;
            yield return new WaitForSecondsRealtime(spawnDelay);
        }

        yield return new WaitForSecondsRealtime(holdDelay);

        int remaining = count;
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(FlyToTarget(icons[i], isLast: i == count - 1, () =>
            {
                remaining--;
                if (remaining == 0)
                {
                    StartCoroutine(PunchScale());
                    onComplete?.Invoke();
                }
            }));
        }
    }

    IEnumerator FlyToTarget(RectTransform rt, bool isLast, Action onDone)
    {
        Vector3 start = rt.position;
        float   t     = 0f;

        while (t < 1f)
        {
            t = Mathf.MoveTowards(t, 1f, Time.unscaledDeltaTime / flyDuration);
            rt.position = Vector3.Lerp(start, collectionPanel.position, t);
            yield return null;
        }

        // Play disappear sound only on the last icon to avoid sound spam
        if (isLast) PlaySound(disappearClip);

        Destroy(rt.gameObject);
        onDone?.Invoke();
    }

    IEnumerator PunchScale()
    {
        Vector3 original = collectionPanel.localScale;
        float   t        = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / punchDuration;
            collectionPanel.localScale = Vector3.Lerp(
                original, original * punchScale, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
            yield return null;
        }

        collectionPanel.localScale = original;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}