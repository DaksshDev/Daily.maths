using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// =============================================================================
//  Data Structures
// =============================================================================

[System.Serializable]
public class Question
{
    public string       displayText;
    public float        answer;
    public string       difficulty;
    public int          difficultyScore;
    public List<string> tags;
    public float        timeAlloted;
    public bool         isFraction;
    public FractionData[] fractions;
    public string       operatorSymbol;

    public Question(string display, float ans, string diff, int diffScore,
                    List<string> t, float time)
    {
        displayText     = display;
        answer          = ans;
        difficulty      = diff;
        difficultyScore = diffScore;
        tags            = t;
        timeAlloted     = time;
        isFraction      = false;
    }
}

[System.Serializable]
public class FractionData
{
    public int numerator;
    public int denominator;
    public FractionData(int num, int den) { numerator = num; denominator = den; }
}

// =============================================================================
//  TagPerformance
// =============================================================================

public class TagPerformance
{
    public int   attempts;
    public int   correct;
    public int   correctCount;
    public float avgCorrectSolveTime;
    public float avgTimeRatio;

    private const int MIN_TRUSTED_SAMPLES = 3;

    public float Accuracy       => attempts == 0 ? 1f : (float)correct / attempts;
    public bool  HasTrustedTime => correctCount >= MIN_TRUSTED_SAMPLES;

    public float WeaknessScore
    {
        get
        {
            if (attempts == 0) return 0f;
            float accuracyPenalty = 1f - Accuracy;
            float speedPenalty    = Mathf.Clamp01(avgTimeRatio - 0.5f);
            return Mathf.Clamp01(accuracyPenalty * 0.7f + speedPenalty * 0.3f);
        }
    }

    public void Record(bool wasCorrect, float elapsed, float allotted)
    {
        attempts++;
        if (wasCorrect) correct++;

        float ratio  = allotted > 0f ? Mathf.Clamp(elapsed / allotted, 0f, 1.5f) : 1f;
        float alphaR = attempts == 1 ? 1f : 0.3f;
        avgTimeRatio = Mathf.Lerp(avgTimeRatio, ratio, alphaR);

        if (wasCorrect)
        {
            correctCount++;
            float alphaT        = correctCount == 1 ? 1f : 0.35f;
            avgCorrectSolveTime = Mathf.Lerp(avgCorrectSolveTime, elapsed, alphaT);
        }
    }
}

// =============================================================================
//  TypeStats
// =============================================================================

public class TypeStats
{
    public int   attempts;
    public int   correct;
    public float totalSolveTime;

    public float Accuracy     => attempts == 0 ? 0f : (float)correct / attempts;
    public float AvgSolveTime => attempts == 0 ? 0f : totalSolveTime / attempts;

    public void Record(bool wasCorrect, float elapsed)
    {
        attempts++;
        if (wasCorrect) correct++;
        totalSolveTime += elapsed;
    }
}

// =============================================================================
//  QuestionGenerator
// =============================================================================

public class QuestionGenerator : MonoBehaviour
{
    [CoolHeader("QuestionGen Algorithm")]
    
    [Space]
    [Header("Dev Mode")]
    public bool devMode            = false;
    [Range(1, 10)] public int devUserIQ = 8;
    public int devDaysSinceInstall = 90;

    [Header("Dynamic Timing Tuning")]
    [Tooltip(
        "How strongly the user's measured solve time steers the allotted time.\n" +
        "0 = fully static.\n1 = fully user-driven.\n0.6 is recommended.")]
    [Range(0f, 1f)]
    public float userTimingWeight = 0.6f;

    [Tooltip("Multiplier on the user's avg correct-solve time to give a generous deadline.")]
    [Range(1.1f, 2.5f)]
    public float solveTimeBuffer = 1.4f;

    // ── Adaptive State ────────────────────────────────────────────────────────
    private readonly Dictionary<string, TagPerformance> _tagPerf   = new();
    private readonly Dictionary<string, TypeStats>      _typeStats  = new();
    private readonly Queue<string>                      _recentTiers = new();
    private const int HISTORY_WINDOW = 5;

    private int   _consecutiveWrong;
    private int   _consecutiveFast;
    private int   _streakModifier;
    private float _sessionAccuracy = 1f;
    private int   _sessionAttempts;

    private readonly Dictionary<string, int> _opCount = new()
    {
        ["Addition"] = 0, ["Subtraction"] = 0,
        ["Multiplication"] = 0, ["Division"] = 0, ["Fractions"] = 0
    };
    private int _totalGenerated;

    private Dictionary<string, List<System.Func<Question>>> _pools;
    private int _daysSinceInstall;
    private int _userIQ;

    // ── Practice-This Topic ───────────────────────────────────────────────────

    // The topic key derived from the HelpfulInfo card (e.g. "Tables", "Squares",
    // "Cubes", "Conversion Chart", or a specific table like "Table of 7").
    private string _practiceThisTopic = "";
    private bool   _hasPracticeThisTopic = false;

    // ==========================================================================
    //  Public API
    // ==========================================================================

    public void Init(int days, int iq)
    {
        _daysSinceInstall = devMode ? devDaysSinceInstall : days;
        _userIQ           = Mathf.Clamp(devMode ? devUserIQ : iq, 1, 10);

        _tagPerf.Clear();
        _typeStats.Clear();
        _recentTiers.Clear();

        foreach (var key in _opCount.Keys.ToList()) _opCount[key] = 0;

        _consecutiveWrong = 0;
        _consecutiveFast  = 0;
        _streakModifier   = 0;
        _sessionAccuracy  = 1f;
        _sessionAttempts  = 0;
        _totalGenerated   = 0;

        BuildPools();
        Debug.Log($"[QGen] Init — days:{_daysSinceInstall}  iq:{_userIQ}");
    }

    /// <summary>
    /// Sets the topic for Practice-This mode.
    /// Pass in the first line / header of the card content so we can parse the topic.
    /// </summary>
    public void SetPracticeThisTopic(string cardContent)
    {
        // Extract the first line which carries the topic header
        // e.g. "Table of 7\n──────────────\n7 x 1 = 7\n..."
        // or   "SQUARES 1-10\n..."
        string firstLine = cardContent.Split('\n')[0].Trim().ToUpper();

        _hasPracticeThisTopic = true;
        _practiceThisTopic    = firstLine;

        // Reset op counts / session state so distribution tracking is fresh
        foreach (var key in _opCount.Keys.ToList()) _opCount[key] = 0;
        _totalGenerated  = 0;
        _sessionAttempts = 0;
        _sessionAccuracy = 1f;

        Debug.Log($"[QGen] Practice-This topic set: '{_practiceThisTopic}'");
    }

    /// <summary>Clears the Practice-This topic so normal generation resumes.</summary>
    public void ClearPracticeThisTopic()
    {
        _hasPracticeThisTopic = false;
        _practiceThisTopic    = "";
        Debug.Log("[QGen] Practice-This topic cleared.");
    }

    public void RecordAnswer(List<string> tags, bool correct, float elapsed, float allotted)
    {
        _sessionAttempts++;

        if (correct)
        {
            _consecutiveWrong = 0;
            bool wasFast = allotted > 0f && (elapsed / allotted) < 0.6f;
            _consecutiveFast = wasFast ? _consecutiveFast + 1 : 0;
        }
        else
        {
            _consecutiveWrong++;
            _consecutiveFast = 0;
        }

        if (_consecutiveFast >= 5 && _streakModifier < 1)
        {
            _streakModifier++;
            _consecutiveFast = 0;
            Debug.Log("[QGen] Fast streak → tier +1");
        }
        if (_consecutiveWrong >= 3 && _streakModifier > -1)
        {
            _streakModifier--;
            _consecutiveWrong = 0;
            Debug.Log("[QGen] Fail streak → tier -1");
        }

        _sessionAccuracy = Mathf.Lerp(_sessionAccuracy, correct ? 1f : 0f, 0.25f);

        foreach (var tag in tags)
        {
            if (!_tagPerf.TryGetValue(tag, out var perf))
                _tagPerf[tag] = perf = new TagPerformance();
            perf.Record(correct, elapsed, allotted);
        }

        if (tags.Count > 0)
        {
            string opKey = tags[0];
            if (!_typeStats.TryGetValue(opKey, out var ts))
                _typeStats[opKey] = ts = new TypeStats();
            ts.Record(correct, elapsed);
        }
    }

    public List<Question> GenerateQuestions(int count = 20)
    {
        var list = new List<Question>(count);
        for (int i = 0; i < count; i++)
            list.Add(GenerateSingle(i, count));
        return list;
    }

    public string GetWeakestTag()
    {
        if (_tagPerf.Count == 0) return "";
        return _tagPerf.OrderByDescending(kv => kv.Value.WeaknessScore).First().Key;
    }

    public Dictionary<string, TypeStats> GetTypeStats() => _typeStats;

    // ==========================================================================
    //  Pool Construction
    // ==========================================================================

    private void BuildPools()
    {
        _pools = new Dictionary<string, List<System.Func<Question>>>
        {
            ["VeryEasy"] = BuildVeryEasyPool(),
            ["Easy"]     = BuildEasyPool(),
            ["Medium"]   = BuildMediumPool(),
            ["Hard"]     = BuildHardPool(),
        };
    }

    private List<System.Func<Question>> BuildVeryEasyPool()
    {
        var p = new List<System.Func<Question>>();
        for (int i = 0; i < 3; i++) p.Add(() => SimpleAdd(1,  9,  1, 9,  "VeryEasy"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleSub(2,  9,  1, 8,  "VeryEasy"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleMul(2,  5,  1, 5,  "VeryEasy"));
        p.Add(()                               => SimpleDiv(1, 4,  2, 5,  "VeryEasy"));
        return p;
    }

    private List<System.Func<Question>> BuildEasyPool()
    {
        var p = new List<System.Func<Question>>();
        for (int i = 0; i < 3; i++) p.Add(() => SimpleAdd(10, 49,  1, 19, "Easy"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleSub(15, 60,  1, 19, "Easy"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleMul(2,  9,   2, 6,  "Easy"));
        p.Add(()                               => SimpleDiv(2, 9,   2, 9,  "Easy"));
        if (_daysSinceInstall >= 5)
        {
            p.Add(() => FracSameDenom("Easy"));
            p.Add(() => FracSameDenom("Easy"));
        }
        return p;
    }

    private List<System.Func<Question>> BuildMediumPool()
    {
        var p = new List<System.Func<Question>>();
        for (int i = 0; i < 2; i++) p.Add(() => IntegerAdd("Medium"));
        for (int i = 0; i < 2; i++) p.Add(() => IntegerSub("Medium"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleMul(6, 15, 6, 9,   "Medium"));
        p.Add(()                               => SimpleDiv(6, 15, 3, 9,  "Medium"));
        if (_daysSinceInstall >= 5)
        {
            for (int i = 0; i < 2; i++) p.Add(() => FracSameDenom("Medium"));
            p.Add(()                           => FracDiffDenom("Medium"));
        }
        return p;
    }

    private List<System.Func<Question>> BuildHardPool()
    {
        var p = new List<System.Func<Question>>();
        for (int i = 0; i < 2; i++) p.Add(() => SimpleAdd(25, 99, 25, 74, "Hard"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleSub(35, 99, 15, 59, "Hard"));
        for (int i = 0; i < 2; i++) p.Add(() => SimpleMul(12, 24,  6,  9, "Hard"));
        p.Add(()                               => SimpleDiv(8, 19,  6,  9, "Hard"));
        p.Add(()                               => NegativeResult());
        p.Add(()                               => NegativeOperand());
        p.Add(()                               => MakeDecimal("Hard"));
        if (_daysSinceInstall >= 5)
        {
            for (int i = 0; i < 2; i++) p.Add(() => FracDiffDenom("Hard"));
            p.Add(()                           => FracMultiply("Hard"));
        }
        return p;
    }

    // ==========================================================================
    //  Core Generation
    // ==========================================================================

    private Question GenerateSingle(int index, int total)
    {
        // ── Practice-This mode: override normal generation ────────────────────
        if (_hasPracticeThisTopic)
        {
            var ptQ = GeneratePracticeThisQuestion(index, total);
            if (ptQ != null)
            {
                TrackOp(ptQ);
                _totalGenerated++;
                Debug.Log($"[QGen] PracticeThis slot={index} tags=[{string.Join(",", ptQ.tags)}]");
                return ptQ;
            }
        }

        // ── Normal generation ─────────────────────────────────────────────────
        string tier = ChooseTier(index, total);

        _recentTiers.Enqueue(tier);
        if (_recentTiers.Count > HISTORY_WINDOW) _recentTiers.Dequeue();

        if (tier == "Hard" && _recentTiers.Count(t => t == "Hard") >= 3)
        {
            tier = "Medium";
            Debug.Log("[QGen] Hard-cap: 3 consecutive → Medium");
        }

        bool blockFractions = _totalGenerated > 0 &&
                              (float)_opCount["Fractions"] / _totalGenerated >= 0.25f;

        if (_sessionAttempts >= 5 && Random.value < WeakTagInjectionChance())
        {
            var weakQ = TryBuildWeakTagQuestion(tier, blockFractions);
            if (weakQ != null)
            {
                TrackOp(weakQ);
                _totalGenerated++;
                Debug.Log($"[QGen] slot={index} WEAK REINFORCEMENT tier={tier} " +
                          $"diff={weakQ.difficultyScore} time={weakQ.timeAlloted:F1}s " +
                          $"tags=[{string.Join(",", weakQ.tags)}]");
                return weakQ;
            }
        }

        var pool = _pools.TryGetValue(tier, out var p) ? p : _pools["Easy"];
        Question q;
        int tries = 0;
        do { q = pool[Random.Range(0, pool.Count)](); tries++; }
        while (blockFractions && q.tags.Contains("Fractions") && tries < 6);

        TrackOp(q);
        _totalGenerated++;
        Debug.Log($"[QGen] slot={index} tier={tier} diff={q.difficultyScore} " +
                  $"time={q.timeAlloted:F1}s tags=[{string.Join(",", q.tags)}]");
        return q;
    }

    // ==========================================================================
    //  Practice-This Generation
    // ==========================================================================

    /// <summary>
    /// Generates a question themed around the active Practice-This topic.
    ///
    /// Topic strings (uppercased first line of the card):
    ///   "TABLE OF N"          → multiplication/division for table N
    ///   "TABLES N–M" etc.     → random table in range
    ///   "SQUARES 1–10" etc.   → n² questions
    ///   "CUBES 1–10" etc.     → n³ questions
    ///   "LENGTH" / "MASS" /
    ///   "VOLUME..." etc.      → unit conversion word problems (arithmetic)
    ///   "FRACTIONS" / any
    ///   fraction card         → fraction questions
    ///   fallback              → mixed arithmetic
    /// </summary>
    private Question GeneratePracticeThisQuestion(int index, int total)
    {
        string topic = _practiceThisTopic;

        // ── Tables ────────────────────────────────────────────────────────────
        if (topic.StartsWith("TABLE OF"))
        {
            // "TABLE OF 7"
            if (int.TryParse(topic.Replace("TABLE OF", "").Trim(), out int n))
                return TableQuestion(n);
        }

        if (topic.StartsWith("TABLES") || topic == "TABLES")
        {
            // "TABLES 1–30" or just "TABLES" — pick a random table 1–12
            int n = Random.Range(1, 13);
            return TableQuestion(n);
        }

        // ── Squares ───────────────────────────────────────────────────────────
        if (topic.StartsWith("SQUARES"))
        {
            // Parse "SQUARES 1–10", "SQUARES 11–20", "SQUARES 21–30"
            ParseRange(topic.Replace("SQUARES", "").Trim(), 1, 30, out int lo, out int hi);
            int n     = Random.Range(lo, hi + 1);
            int score = ComputeDiffScore(n >= 10 ? 1 : 0);
            float time = ComputeDynamicTime(score, "Multiplication");
            return new Question($"{n}² = ?", (float)(n * n), "Medium", score,
                new List<string> { "Multiplication", "squares" }, time);
        }

        // ── Cubes ─────────────────────────────────────────────────────────────
        if (topic.StartsWith("CUBES"))
        {
            ParseRange(topic.Replace("CUBES", "").Trim(), 1, 20, out int lo, out int hi);
            int n     = Random.Range(lo, hi + 1);
            int score = ComputeDiffScore(n >= 5 ? 2 : 1);
            float time = ComputeDynamicTime(score, "Multiplication");
            return new Question($"{n}³ = ?", (float)((long)n * n * n), "Hard", score,
                new List<string> { "Multiplication", "cubes" }, time);
        }

        // ── Fractions ─────────────────────────────────────────────────────────
        if (topic.Contains("FRACTION"))
        {
            bool diffDenom = Random.value < 0.5f;
            return diffDenom ? FracDiffDenom("Medium") : FracSameDenom("Medium");
        }

        // ── Conversion topics → arithmetic word-problem style ─────────────────
        // For LENGTH, MASS, VOLUME, TIME, TEMPERATURE, AREA, SPEED, PRESSURE, ENERGY
        // we generate arithmetic that mirrors the mental arithmetic involved in conversions.
        if (topic == "LENGTH")   return ConversionArithmetic(1000, "m", "km");
        if (topic == "MASS")     return ConversionArithmetic(1000, "g",  "kg");
        if (topic.StartsWith("VOLUME")) return ConversionArithmetic(1000, "mL", "L");
        if (topic == "TIME")     return ConversionArithmetic(60,   "s",  "min");
        if (topic == "AREA")     return ConversionArithmetic(10000,"cm²","m²");
        if (topic == "SPEED")    return ConversionArithmetic(36,   "km/h","m/s (×10)");
        if (topic == "PRESSURE") return ConversionArithmetic(1000, "Pa", "kPa");
        if (topic == "ENERGY")   return ConversionArithmetic(1000, "J",  "kJ");

        // ── Temperature ───────────────────────────────────────────────────────
        if (topic == "TEMPERATURE") return TemperatureQuestion();

        // ── Fallback — treat as mixed multiplication ──────────────────────────
        return SimpleMul(2, 12, 2, 12, "Medium");
    }

    // ── Practice-This helpers ─────────────────────────────────────────────────

    /// <summary>Generates a multiplication or division question for table N.</summary>
    private Question TableQuestion(int n)
    {
        bool divide = Random.value < 0.4f; // 40 % chance of asking as division

        if (divide)
        {
            int q     = Random.Range(1, 11);
            int a     = n * q;
            int score = ComputeDiffScore(1);
            float time = ComputeDynamicTime(score, "Division");
            return new Question($"{a} ÷ {n}", q, "Easy", score,
                new List<string> { "Division", $"table_{n}" }, time);
        }
        else
        {
            int b     = Random.Range(1, 11);
            bool hardFactor = n >= 7 || b >= 7;
            int score = ComputeDiffScore(hardFactor ? 1 : 0);
            float time = ComputeDynamicTime(score, "Multiplication");
            return new Question($"{n} × {b}", n * b, "Easy", score,
                new List<string> { "Multiplication", $"table_{n}" }, time);
        }
    }

    /// <summary>
    /// Generates a simple arithmetic question that reflects unit conversion ratios.
    /// e.g. for LENGTH: "3 500 m → __ km?" (answer: 3.5)
    /// </summary>
    private Question ConversionArithmetic(int ratio, string fromUnit, string toUnit)
    {
        bool multiply = Random.value < 0.5f;
        int score     = ComputeDiffScore(1);
        float time    = ComputeDynamicTime(score, "Division");

        if (multiply)
        {
            // How many fromUnit in N toUnit?
            int n   = Random.Range(1, 11);
            int ans = n * ratio;
            return new Question($"{n} {toUnit} → {fromUnit}?", ans, "Medium", score,
                new List<string> { "Multiplication", "conversion" }, time);
        }
        else
        {
            // Convert N*ratio fromUnit to toUnit
            int n   = Random.Range(1, 11);
            int val = n * ratio;
            return new Question($"{val} {fromUnit} → {toUnit}?", n, "Medium", score,
                new List<string> { "Division", "conversion" }, time);
        }
    }

    /// <summary>Celsius ↔ Kelvin offset questions (simple addition/subtraction).</summary>
    private Question TemperatureQuestion()
    {
        bool toKelvin = Random.value < 0.5f;
        int  celsius  = Random.Range(-50, 101);
        int  score    = ComputeDiffScore(0);
        float time    = ComputeDynamicTime(score, "Addition");

        if (toKelvin)
            return new Question($"{celsius}°C → K?", celsius + 273, "Medium", score,
                new List<string> { "Addition", "temperature" }, time);
        else
        {
            int kelvin = celsius + 273;
            return new Question($"{kelvin} K → °C?", celsius, "Medium", score,
                new List<string> { "Subtraction", "temperature" }, time);
        }
    }

    /// <summary>
    /// Parses a range string like "1–10" or "11-20". Falls back to defaults on failure.
    /// </summary>
    private static void ParseRange(string s, int defaultLo, int defaultHi,
                                   out int lo, out int hi)
    {
        lo = defaultLo;
        hi = defaultHi;
        if (string.IsNullOrEmpty(s)) return;

        // Accept both '–' (en-dash) and '-' (hyphen)
        string normalized = s.Replace("–", "-").Replace("—", "-");
        var parts = normalized.Split('-');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out int a) &&
            int.TryParse(parts[1].Trim(), out int b))
        {
            lo = a;
            hi = b;
        }
    }

    private void TrackOp(Question q)
    {
        if (q.tags.Count == 0) return;
        string op = q.tags[0];
        if (_opCount.ContainsKey(op)) _opCount[op]++;
    }

    // ==========================================================================
    //  Tier Selection
    // ==========================================================================

    private string ChooseTier(int index, int total)
    {
        bool allowMedium = _daysSinceInstall >= 3;
        bool allowHard   = _daysSinceInstall >= 7;

        float slotProgress  = (float)index / total;
        float dayBoost      = Mathf.Clamp01(_daysSinceInstall / 30f) * 0.4f;
        float iqBoost       = (_userIQ - 5f) / 10f;
        float accuracyDelta = (_sessionAccuracy - 0.7f) * 0.25f;
        float streakDelta   = _streakModifier * 0.15f;

        float effective = Mathf.Clamp01(
            slotProgress + iqBoost * 0.3f + dayBoost + accuracyDelta + streakDelta);

        if (!allowMedium || effective < 0.25f) return "VeryEasy";
        if (!allowHard   || effective < 0.50f) return "Easy";
        if (effective    < 0.78f)              return "Medium";
        return "Hard";
    }

    // ==========================================================================
    //  Weak-Tag Injection
    // ==========================================================================

    private float WeakTagInjectionChance()
        => Mathf.Clamp((_sessionAttempts - 5f) / 50f, 0f, 0.35f);

    private Question TryBuildWeakTagQuestion(string tier, bool blockFractions)
    {
        if (_tagPerf.Count == 0) return null;

        var candidate = _tagPerf
            .Where(kv  => kv.Value.attempts >= 3 && kv.Value.WeaknessScore > 0.35f)
            .OrderByDescending(kv => kv.Value.WeaknessScore)
            .Select(kv => kv.Key)
            .FirstOrDefault();

        if (candidate == null) return null;
        if (candidate == "Fractions" && blockFractions) return null;
        return BuildQuestionForTag(candidate, tier);
    }

    private Question BuildQuestionForTag(string tag, string tier) => tag switch
    {
        "Addition"       => SimpleAdd(10, 49, 1, 19, tier),
        "Subtraction"    => SimpleSub(15, 60, 1, 19, tier),
        "Multiplication" => SimpleMul(2,  12, 2,  9, tier),
        "Division"       => SimpleDiv(2,  10, 2,  9, tier),
        "Integers"       => IntegerAdd(tier),
        "Decimals"       => MakeDecimal(tier),
        "Fractions"      => _daysSinceInstall >= 5 ? FracSameDenom(tier)
                                                   : SimpleAdd(10, 49, 1, 19, tier),
        _                => null,
    };

    // ==========================================================================
    //  Difficulty Score
    // ==========================================================================

    private static int ComputeDiffScore(int bonuses) => Mathf.Clamp(1 + bonuses, 1, 5);

    // ==========================================================================
    //  Dynamic Time Allocation
    // ==========================================================================

    private static readonly (float min, float max)[] BaseTimeRange =
    {
        (0f,  0f),
        (2f,  3f),
        (3f,  5f),
        (5f,  7f),
        (7f,  9f),
        (9f, 12f),
    };

    private float ComputeDynamicTime(int score, string primaryTag)
    {
        int si = Mathf.Clamp(score, 1, 5);
        var (bMin, bMax) = BaseTimeRange[si];
        float staticMid  = (bMin + bMax) * 0.5f;

        float userTime   = staticMid;
        float trustLevel = 0f;

        if (!string.IsNullOrEmpty(primaryTag) &&
            _tagPerf.TryGetValue(primaryTag, out var perf) &&
            perf.HasTrustedTime)
        {
            userTime   = perf.avgCorrectSolveTime * solveTimeBuffer;
            trustLevel = Mathf.Clamp01((perf.correctCount - 3f) / 7f);
        }

        float effectiveWeight = userTimingWeight * trustLevel;
        float blended         = Mathf.Lerp(staticMid, userTime, effectiveWeight);
        float clamped         = Mathf.Clamp(blended, bMin * 0.8f, bMax * 1.2f);
        clamped               = Mathf.Clamp(clamped, 2f, 12f);
        return Mathf.Round(clamped * 2f) / 2f;
    }

    // ==========================================================================
    //  Question Factories
    // ==========================================================================

    private Question SimpleAdd(int aMin, int aMax, int bMin, int bMax, string diff)
    {
        int  a     = Random.Range(aMin, aMax + 1), b = Random.Range(bMin, bMax + 1);
        bool carry = HasCarry(a, b);
        int  score = ComputeDiffScore(carry ? 1 : 0);
        float time = ComputeDynamicTime(score, "Addition");

        return new Question($"{a} + {b}", a + b, diff, score,
            new List<string> { "Addition",
                carry ? "multi_digit_addition_with_carry" : "multi_digit_addition_no_carry" },
            time);
    }

    private Question SimpleSub(int aMin, int aMax, int bMin, int bMax, string diff)
    {
        int  b      = Random.Range(bMin, bMax + 1);
        int  a      = Random.Range(Mathf.Max(aMin, b), aMax + 1);
        bool borrow = NeedsBorrow(a, b);
        int  score  = ComputeDiffScore(borrow ? 1 : 0);
        float time  = ComputeDynamicTime(score, "Subtraction");

        return new Question($"{a} - {b}", a - b, diff, score,
            new List<string> { "Subtraction",
                borrow ? "borrowing_required" : "basic_subtraction" },
            time);
    }

    private Question SimpleMul(int aMin, int aMax, int bMin, int bMax, string diff)
    {
        int  a          = Mathf.Clamp(Random.Range(aMin, aMax + 1), aMin, 99);
        int  b          = Mathf.Clamp(Random.Range(bMin, bMax + 1), bMin, 9);
        bool hardFactor = a >= 7 || b >= 7;
        int  score      = ComputeDiffScore(hardFactor ? 1 : 0);
        float time      = ComputeDynamicTime(score, "Multiplication");

        return new Question($"{a} × {b}", a * b, diff, score,
            new List<string> { "Multiplication",
                (a >= 10 || b >= 10) ? "multiplication_multi_digit" : "multiplication_single_digit" },
            time);
    }

    private Question SimpleDiv(int bMin, int bMax, int qMin, int qMax, string diff)
    {
        int   b     = Random.Range(bMin, bMax + 1);
        int   q     = Random.Range(qMin, qMax + 1);
        int   a     = b * q;
        int   score = ComputeDiffScore(1);
        float time  = ComputeDynamicTime(score, "Division");

        return new Question($"{a} ÷ {b}", q, diff, score,
            new List<string> { "Division", "division_basic" }, time);
    }

    private Question IntegerAdd(string diff)
    {
        int   a     = Random.Range(-20, 30), b = Random.Range(-20, 20);
        float ans   = a + b;
        int   score = ComputeDiffScore(a < 0 || b < 0 ? 1 : 0);
        float time  = ComputeDynamicTime(score, "Integers");

        return new Question($"({a}) + ({b})", ans, diff, score,
            new List<string> { "Integers", "basic_addition" }, time);
    }

    private Question IntegerSub(string diff)
    {
        int   a     = Random.Range(-20, 40), b = Random.Range(-20, 30);
        float ans   = a - b;
        int   score = ComputeDiffScore(a < 0 || b < 0 ? 1 : 0);
        float time  = ComputeDynamicTime(score, "Integers");

        return new Question($"({a}) - ({b})", ans, diff, score,
            new List<string> { "Integers", "basic_subtraction" }, time);
    }

    private Question NegativeResult()
    {
        int a     = Random.Range(1, 20), b = Random.Range(a + 1, a + 15);
        int score = ComputeDiffScore(1);
        return new Question($"{a} - {b}", a - b, "Hard", score,
            new List<string> { "Integers", "basic_subtraction" },
            ComputeDynamicTime(score, "Integers"));
    }

    private Question NegativeOperand()
    {
        int  a   = -Random.Range(1, 15), b = Random.Range(1, 15);
        bool add = Random.value < 0.5f;
        int  score = ComputeDiffScore(1);
        return new Question($"{a} {(add ? "+" : "-")} {b}", add ? a + b : a - b,
            "Hard", score, new List<string> { "Integers" },
            ComputeDynamicTime(score, "Integers"));
    }

    private Question MakeDecimal(string diff)
    {
        float a    = Mathf.Round(Random.Range(1f, 15f) * 4f) / 4f;
        float b    = Mathf.Round(Random.Range(1f, 8f)  * 4f) / 4f;
        bool  add  = Random.value < 0.5f;
        float ans  = Mathf.Round((add ? a + b : a - b) * 100f) / 100f;
        int   score = ComputeDiffScore(1);
        float time  = ComputeDynamicTime(score, "Decimals");

        return new Question($"{a} {(add ? "+" : "-")} {b}", ans, diff, score,
            new List<string> { "Decimals" }, time);
    }

    // ── Fraction Factories ────────────────────────────────────────────────────

    private Question FracSameDenom(string tier)
    {
        int  denom = Random.Range(2, 9);
        int  nA    = Random.Range(1, denom), nB = Random.Range(1, denom);
        bool add   = tier == "Easy" || Random.value < 0.6f;
        if (!add && nB > nA) (nA, nB) = (nB, nA);

        float ans   = Mathf.Round((add ? (float)(nA + nB) / denom
                                       : (float)(nA - nB) / denom) * 1000f) / 1000f;
        int   score = ComputeDiffScore(1);
        float time  = ComputeDynamicTime(score, "Fractions");

        return MakeFracQ(nA, denom, nB, denom, add ? "+" : "-", ans, tier, score,
            new List<string> { "Fractions", "fraction_addition_same_denominator" }, time);
    }

    private Question FracDiffDenom(string tier)
    {
        int dA = Random.Range(2, 7), dB = Random.Range(2, 7);
        while (dB == dA) dB = Random.Range(2, 7);
        int  nA   = Random.Range(1, dA), nB = Random.Range(1, dB);
        bool add  = Random.value < 0.6f;
        float fa  = (float)nA / dA, fb = (float)nB / dB;
        float ans = Mathf.Round((add ? fa + fb : fa - fb) * 1000f) / 1000f;

        int   score = ComputeDiffScore(2);
        float time  = ComputeDynamicTime(score, "Fractions");

        return MakeFracQ(nA, dA, nB, dB, add ? "+" : "-", ans, tier, score,
            new List<string> { "Fractions", "fraction_addition_cross_multiply" }, time);
    }

    private Question FracMultiply(string tier)
    {
        int   dA  = Random.Range(2, 8), dB = Random.Range(2, 8);
        int   nA  = Random.Range(1, dA + 1), nB = Random.Range(1, dB + 1);
        float ans = Mathf.Round((float)(nA * nB) / (dA * dB) * 1000f) / 1000f;

        int   score = ComputeDiffScore(1);
        float time  = ComputeDynamicTime(score, "Fractions");

        return MakeFracQ(nA, dA, nB, dB, "×", ans, tier, score,
            new List<string> { "Fractions", "fraction_simplification" }, time);
    }

    private Question MakeFracQ(int nA, int dA, int nB, int dB,
        string sym, float ans, string tier, int score, List<string> tags, float time)
    {
        var q = new Question($"Fraction: {nA}/{dA} {sym} {nB}/{dB}",
            ans, tier, score, tags, time);
        q.isFraction     = true;
        q.fractions      = new[] { new FractionData(nA, dA), new FractionData(nB, dB) };
        q.operatorSymbol = sym;
        return q;
    }

    // ==========================================================================
    //  Math Helpers
    // ==========================================================================

    private static bool HasCarry(int a, int b)
    {
        while (a > 0 || b > 0)
        {
            if ((a % 10) + (b % 10) >= 10) return true;
            a /= 10; b /= 10;
        }
        return false;
    }

    private static bool NeedsBorrow(int a, int b)
    {
        while (a > 0 || b > 0)
        {
            if ((a % 10) < (b % 10)) return true;
            a /= 10; b /= 10;
        }
        return false;
    }

    private static int GCD(int a, int b)
    {
        while (b != 0) { int t = b; b = a % b; a = t; }
        return a;
    }
}