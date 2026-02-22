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
    public string       difficulty;       // "VeryEasy" | "Easy" | "Medium" | "Hard"
    public int          difficultyScore;  // 1–5 mental-effort score
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
//  TagPerformance  — per-operation-type solve-time + accuracy tracking
// =============================================================================

public class TagPerformance
{
    public int   attempts;
    public int   correct;

    // EMA of raw seconds on CORRECT answers only (wrong/skip time is noise)
    public int   correctCount;
    public float avgCorrectSolveTime;

    // EMA of elapsed/allotted across all attempts (for weakness scoring)
    public float avgTimeRatio;

    // We need at least this many correct samples before trusting the timing data
    private const int MIN_TRUSTED_SAMPLES = 3;

    // ── Derived ───────────────────────────────────────────────────────────────

    public float Accuracy     => attempts == 0 ? 1f : (float)correct / attempts;
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

    // ── Recording ─────────────────────────────────────────────────────────────

    public void Record(bool wasCorrect, float elapsed, float allotted)
    {
        attempts++;
        if (wasCorrect) correct++;

        // Time-ratio EMA (all attempts)
        float ratio  = allotted > 0f ? Mathf.Clamp(elapsed / allotted, 0f, 1.5f) : 1f;
        float alphaR = attempts == 1 ? 1f : 0.3f;
        avgTimeRatio = Mathf.Lerp(avgTimeRatio, ratio, alphaR);

        // Correct-solve-time EMA (signal-only — ignore wrong/skip noise)
        if (wasCorrect)
        {
            correctCount++;
            float alphaT        = correctCount == 1 ? 1f : 0.35f;
            avgCorrectSolveTime = Mathf.Lerp(avgCorrectSolveTime, elapsed, alphaT);
        }
    }
}

// =============================================================================
//  TypeStats  — session summary exposed for the end-screen
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
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Dev Mode")]
    public bool devMode            = false;
    [Range(1, 10)] public int devUserIQ = 8;
    public int devDaysSinceInstall = 90;

    [Header("Dynamic Timing Tuning")]
    [Tooltip(
        "How strongly the user's measured solve time steers the allotted time.\n" +
        "0 = fully static (difficulty score only).\n" +
        "1 = fully user-driven (user data only).\n" +
        "0.6 is the recommended starting value.")]
    [Range(0f, 1f)]
    public float userTimingWeight = 0.6f;

    [Tooltip(
        "Multiplier on the user's avg correct-solve time to set a generous but " +
        "pressured deadline.  1.4 = 40 % more time than they usually take.")]
    [Range(1.1f, 2.5f)]
    public float solveTimeBuffer = 1.4f;

    // ── Adaptive State (RAM only — never touches PlayerPrefs mid-session) ─────
    private readonly Dictionary<string, TagPerformance> _tagPerf   = new();
    private readonly Dictionary<string, TypeStats>      _typeStats  = new();
    private readonly Queue<string>                      _recentTiers = new();
    private const int HISTORY_WINDOW = 5;

    private int   _consecutiveWrong;
    private int   _consecutiveFast;
    private int   _streakModifier;      // –1 | 0 | +1 applied to tier selection
    private float _sessionAccuracy = 1f;
    private int   _sessionAttempts;

    // Operation-type distribution guard
    private readonly Dictionary<string, int> _opCount = new()
    {
        ["Addition"] = 0, ["Subtraction"] = 0,
        ["Multiplication"] = 0, ["Division"] = 0, ["Fractions"] = 0
    };
    private int _totalGenerated;

    private Dictionary<string, List<System.Func<Question>>> _pools;
    private int _daysSinceInstall;
    private int _userIQ;

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
    /// Call from GameManager after EVERY question result.
    /// <paramref name="elapsed"/>  = actual seconds from question load → answer/skip.
    /// <paramref name="allotted"/> = timeAlloted on the Question that was shown.
    /// </summary>
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

        // Streak modifier — single-step only, no level skipping
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

        // Feed per-tag performance (this is what drives dynamic timing)
        foreach (var tag in tags)
        {
            if (!_tagPerf.TryGetValue(tag, out var perf))
                _tagPerf[tag] = perf = new TagPerformance();
            perf.Record(correct, elapsed, allotted);
        }

        // Session summary for the end-screen
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

    /// <summary>Returns per-type stats for the end-screen accuracy/speed breakdown.</summary>
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
            p.Add(() => FracSameDenom("Easy")); // 2× weight for practice
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
        string tier = ChooseTier(index, total);

        _recentTiers.Enqueue(tier);
        if (_recentTiers.Count > HISTORY_WINDOW) _recentTiers.Dequeue();

        // Rule: no 3 consecutive Hard questions
        if (tier == "Hard" && _recentTiers.Count(t => t == "Hard") >= 3)
        {
            tier = "Medium";
            Debug.Log("[QGen] Hard-cap: 3 consecutive → Medium");
        }

        // Rule: fractions ≤ 25 % of total
        bool blockFractions = _totalGenerated > 0 &&
                              (float)_opCount["Fractions"] / _totalGenerated >= 0.25f;

        // Weak-tag reinforcement (after warm-up, capped at 35 %)
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

        // Normal pool draw — re-roll if fractions are capped
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
    //  Difficulty Score  (1–5, mental effort)
    // ==========================================================================

    /// <summary>
    /// Base = 1.  Bonuses are additive:
    ///   +1  carry required
    ///   +1  borrow required
    ///   +1  multiplication involves 7–9
    ///   +1  division (inherently harder — always passed explicitly)
    ///   +2  fraction with different denominators
    /// Result clamped to 1–5.
    /// </summary>
    private static int ComputeDiffScore(int bonuses) => Mathf.Clamp(1 + bonuses, 1, 5);

    // ==========================================================================
    //  Dynamic Time Allocation  — THE CORE OF THIS UPGRADE
    // ==========================================================================

    // Static baseline midpoints per difficulty score (seconds).
    // Shape: score 1 → ~2.5 s, score 5 → ~10.5 s.
    // Used as the fallback when no user data exists and as the structural anchor
    // that prevents user data from distorting timing beyond reasonable bounds.
    private static readonly (float min, float max)[] BaseTimeRange =
    {
        (0f,  0f),   // index 0 — unused
        (2f,  3f),   // score 1
        (3f,  5f),   // score 2
        (5f,  7f),   // score 3
        (7f,  9f),   // score 4
        (9f, 12f),   // score 5
    };

    /// <summary>
    /// Computes allotted time by blending two signals:
    ///
    ///   A) Static baseline  — midpoint of the difficulty-score band.
    ///      Always present; acts as structural floor/ceiling.
    ///
    ///   B) User-measured time — user's EMA of correct solve times for this
    ///      operation type × <see cref="solveTimeBuffer"/> (comfortable headroom).
    ///
    /// Blend weight =  <see cref="userTimingWeight"/>  ×  trustLevel,
    /// where trustLevel ramps 0 → 1 as correct samples accumulate (3–10).
    /// This prevents noisy early data from warping the clock too soon.
    ///
    /// Guardrails:
    ///   • Result is clamped within [bandMin × 0.8, bandMax × 1.2] so user data
    ///     can nudge but never escape the difficulty band entirely.
    ///   • Absolute limits: 2 s – 12 s.
    ///   • Rounded to 0.5 s increments for a clean timer display.
    /// </summary>
    private float ComputeDynamicTime(int score, string primaryTag)
    {
        // ── A: Static baseline ────────────────────────────────────────────────
        int si = Mathf.Clamp(score, 1, 5);
        var (bMin, bMax) = BaseTimeRange[si];
        float staticMid  = (bMin + bMax) * 0.5f;

        // ── B: User-measured time (correct-answers only) ──────────────────────
        float userTime   = staticMid; // default until trusted data exists
        float trustLevel = 0f;

        if (!string.IsNullOrEmpty(primaryTag) &&
            _tagPerf.TryGetValue(primaryTag, out var perf) &&
            perf.HasTrustedTime)
        {
            // avgCorrectSolveTime is raw seconds the user actually needed.
            // Multiply by buffer so the deadline is achievable but still pressures.
            userTime = perf.avgCorrectSolveTime * solveTimeBuffer;

            // Trust scales from 0 at 3 samples to 1 at 10 samples.
            // Below 3 we don't use user data at all (HasTrustedTime guards this).
            trustLevel = Mathf.Clamp01((perf.correctCount - 3f) / 7f);
        }

        // ── Blend ─────────────────────────────────────────────────────────────
        float effectiveWeight = userTimingWeight * trustLevel;
        float blended         = Mathf.Lerp(staticMid, userTime, effectiveWeight);

        // ── Soft band clamping ────────────────────────────────────────────────
        // User data may push UP to 20 % beyond band max (slow learners get mercy).
        // User data may pull DOWN to 80 % of band min (fast learners stay challenged).
        float clamped = Mathf.Clamp(blended, bMin * 0.8f, bMax * 1.2f);

        // ── Hard global limits ────────────────────────────────────────────────
        clamped = Mathf.Clamp(clamped, 2f, 12f);

        // ── Round to 0.5 s increments ─────────────────────────────────────────
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
        int  b          = Mathf.Clamp(Random.Range(bMin, bMax + 1), bMin, 9); // 2-digit × 1-digit
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
        // Division is structurally harder — always starts at score 2 (+1 bonus)
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
        int score = ComputeDiffScore(1); // crossing zero = bonus
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
        int   score = ComputeDiffScore(1); // decimals always bump up
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
        int   score = ComputeDiffScore(1); // fractions: base +1
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

        int   score = ComputeDiffScore(2); // different denominator = +2
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