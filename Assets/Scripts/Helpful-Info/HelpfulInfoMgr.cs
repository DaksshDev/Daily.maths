using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class HelpfulInfoMgr : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homeScreenPanel;
    [SerializeField] private GameObject helpfulInfoPanel;

    [Header("Dropdown")]
    [SerializeField] private TMP_Dropdown categoryDropdown;

    [Header("Card Prefab & Container")]
    [SerializeField] private GameObject    cardPrefab;
    [SerializeField] private RectTransform container;

    [Header("Nav")]
    [SerializeField] private Button   prevButton;
    [SerializeField] private Button   nextButton;
    [SerializeField] private TMP_Text counterText;

    [Header("Swipe")]
    [SerializeField] private float swipeThreshold    = 50f;
    [SerializeField] private float verticalTolerance = 60f;

    [Header("Slide")]
    [SerializeField] private float cardWidth     = 1080f;
    [SerializeField] private float slideDuration = 0.28f;

    [Header("Practice-This")]
    [Tooltip("Reference to the GameManager so we can launch Practice-This mode")]
    [SerializeField] private GameManager gameManager;

    private List<string> currentCards = new List<string>();
    private int          currentIndex = 0;
    private bool         animating    = false;
    private Vector2      swipeStart;
    private bool         tracking;

    private HorizontalLayoutGroup layoutGroup;

    private static string Sup(string s) => $"<sup>{s}</sup>";

    void Start()
    {
        layoutGroup = container.GetComponent<HorizontalLayoutGroup>();

        categoryDropdown.ClearOptions();
        categoryDropdown.AddOptions(new List<string> { "Tables", "Squares", "Cubes", "Conversion Chart" });
        categoryDropdown.onValueChanged.AddListener(OnDropdownChanged);

        prevButton.onClick.AddListener(() => TrySlide(false));
        nextButton.onClick.AddListener(() => TrySlide(true));

        helpfulInfoPanel.SetActive(false);
        LoadCategory(0);
    }

    void Update()
    {
        if (!helpfulInfoPanel.activeSelf) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        if      (Input.GetMouseButtonDown(0))           { swipeStart = Input.mousePosition; tracking = true; }
        else if (Input.GetMouseButtonUp(0) && tracking) { tracking = false; EvaluateSwipe(Input.mousePosition); }
#else
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if      (t.phase == TouchPhase.Began)                                                  { swipeStart = t.position; tracking = true; }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && tracking) { tracking = false; EvaluateSwipe(t.position); }
        }
#endif
    }

    public void OpenHelpfulInfo()
    {
        if (homeScreenPanel) homeScreenPanel.SetActive(false);
        helpfulInfoPanel.SetActive(true);
        LoadCategory(categoryDropdown.value);
    }

    public void CloseHelpfulInfo()
    {
        helpfulInfoPanel.SetActive(false);
        if (homeScreenPanel) homeScreenPanel.SetActive(true);
    }

    private void OnDropdownChanged(int index) => LoadCategory(index);

    private void LoadCategory(int index)
    {
        StopAllCoroutines();
        animating    = false;
        tracking     = false;
        currentIndex = 0;

        switch (index)
        {
            case 0: currentCards = BuildTables();      break;
            case 1: currentCards = BuildSquares();     break;
            case 2: currentCards = BuildCubes();       break;
            case 3: currentCards = BuildConversions(); break;
        }

        foreach (Transform child in container)
            Destroy(child.gameObject);

        for (int i = 0; i < currentCards.Count; i++)
        {
            GameObject card = Instantiate(cardPrefab, container);
            RectTransform rt = card.GetComponent<RectTransform>();

            rt.anchoredPosition = new Vector2(i * cardWidth, 0f);

            card.transform.Find("text").GetComponent<TMP_Text>().text = currentCards[i];

            Button btn = card.transform.Find("button").GetComponent<Button>();
            string captured = currentCards[i];

            // Wire the "Practice This" button to launch the game in Practice-This mode
            btn.onClick.AddListener(() => OnPracticeClicked(captured));
        }
        
        RefreshNav();
    }
    
    private void RefreshNav()
    {
        counterText.text        = $"{currentIndex + 1} / {currentCards.Count}";
        prevButton.interactable = currentIndex > 0;
        nextButton.interactable = currentIndex < currentCards.Count - 1;
    }

    private void EvaluateSwipe(Vector2 end)
    {
        float dx = end.x - swipeStart.x;
        float dy = end.y - swipeStart.y;
        if (Mathf.Abs(dy) > verticalTolerance) return;
        if      (dx < -swipeThreshold) TrySlide(true);
        else if (dx >  swipeThreshold) TrySlide(false);
    }

    private void TrySlide(bool forward)
    {
        if (animating) return;
        if ( forward && currentIndex >= currentCards.Count - 1) return;
        if (!forward && currentIndex <= 0)                      return;
        StartCoroutine(SlideAnim(forward));
    }

    private IEnumerator SlideAnim(bool forward)
    {
        animating = true;
        currentIndex += forward ? 1 : -1;

        currentIndex = Mathf.Clamp(currentIndex, 0, currentCards.Count - 1);

        Vector2 from = container.anchoredPosition;
        Vector2 to   = new Vector2(-currentIndex * cardWidth, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / slideDuration, 1f);
            container.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseInOutCubic(t));
            yield return null;
        }

        container.anchoredPosition = to;
        RefreshNav();
        animating = false;
    }

    private static float EaseInOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    // ── Content Builders ──────────────────────────────────────────────────────

    private List<string> BuildTables()
    {
        var list = new List<string>();
        for (int i = 1; i <= 30; i++)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Table of {i}\n──────────────");
            for (int j = 1; j <= 10; j++)
                sb.AppendLine($"{i} x {j} = {i * j}");
            list.Add(sb.ToString().TrimEnd());
        }
        return list;
    }

    private List<string> BuildSquares()
    {
        var list = new List<string>();
        for (int start = 1; start <= 30; start += 10)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Squares {start}–{start + 9}\n──────────────");
            for (int i = start; i <= start + 9; i++)
                sb.AppendLine($"{i}{Sup("2")} = {i * i}");
            list.Add(sb.ToString().TrimEnd());
        }
        return list;
    }

    private List<string> BuildCubes()
    {
        var list = new List<string>();
        int[][] pages = { new[] { 1, 10 }, new[] { 11, 20 } };
        foreach (var p in pages)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Cubes {p[0]}–{p[1]}\n──────────────");
            for (int i = p[0]; i <= p[1]; i++)
                sb.AppendLine($"{i}{Sup("3")} = {(long)i * i * i}");
            list.Add(sb.ToString().TrimEnd());
        }
        return list;
    }

    private List<string> BuildConversions()
    {
        return new List<string>
        {
            "LENGTH\n──────────────\n" +
            "1 km   = 1 000 m\n1 m    = 100 cm = 1 000 mm\n" +
            "1 cm   = 0.01 m\n1 mm   = 0.001 m\n" +
            "1 in   = 2.54 cm\n1 ft   = 12 in = 30.48 cm\n" +
            "1 yd   = 3 ft = 91.44 cm\n1 mi   = 1 609.344 m",

            "MASS\n──────────────\n" +
            "1 t    = 1 000 kg\n1 kg   = 1 000 g\n" +
            "1 g    = 1 000 mg\n1 lb   = 453.592 g\n" +
            "1 oz   = 28.3495 g\n1 US ton = 907.185 kg",

            "VOLUME / CAPACITY\n──────────────\n" +
            "1 L    = 1 000 mL\n" +
            "1 mL   = 1 cm" + Sup("3") + "\n" +
            "1 m"   + Sup("3") + "    = 1 000 L\n" +
            "1 cm"  + Sup("3") + "  = 0.001 L = 1 mL",

            "TIME\n──────────────\n" +
            "1 min  = 60 s\n1 h    = 60 min = 3 600 s\n" +
            "1 d    = 24 h = 86 400 s\n1 wk   = 7 d\n1 yr   = 365 d",

            "TEMPERATURE\n──────────────\n" +
            "K      = °C + 273.15\n" +
            "°F     = °C × 9/5 + 32\n" +
            "°C     = (°F − 32) × 5/9\n" +
            "0°C    = 273.15 K = 32°F\n100°C = 373.15 K = 212°F",

            "AREA\n──────────────\n" +
            "1 km"  + Sup("2") + "  = 1 000 000 m" + Sup("2") + "\n" +
            "1 m"   + Sup("2") + "   = 10 000 cm"  + Sup("2") + "\n" +
            "1 cm"  + Sup("2") + "  = 100 mm"      + Sup("2") + "\n" +
            "1 ha   = 10 000 m" + Sup("2") + "\n1 ac   = 4 046.856 m" + Sup("2"),

            "SPEED\n──────────────\n" +
            "1 m/s  = 3.6 km/h\n1 km/h = 0.2778 m/s\n" +
            "1 mph  = 1.60934 km/h\n1 mph  = 0.44704 m/s\n1 m/s  = 2.23694 mph",

            "PRESSURE\n──────────────\n" +
            "1 Pa   = 1 N/m" + Sup("2") + "\n" +
            "1 kPa  = 1 000 Pa\n1 bar  = 100 000 Pa\n" +
            "1 atm  = 101 325 Pa\n1 atm  = 760 mmHg\n1 mmHg = 133.322 Pa",

            "ENERGY\n──────────────\n" +
            "1 kJ   = 1 000 J\n1 cal  = 4.184 J\n" +
            "1 kcal = 4 184 J\n1 kWh  = 3 600 000 J\n" +
            "1 eV   = 1.602 × 10" + Sup("-19") + " J"
        };
    }

    // ── Practice-This ─────────────────────────────────────────────────────────

    private void OnPracticeClicked(string cardContent)
    {
        if (gameManager == null)
        {
            Debug.LogError("[HelpfulInfoMgr] GameManager reference not assigned — cannot start Practice-This.");
            return;
        }

        // Hide the helpful info panel before launching the game
        helpfulInfoPanel.SetActive(false);

        // Hand off to GameManager — it will handle countdown → questions → results
        gameManager.StartPracticeThis(cardContent);
    }
}