using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime bridge — defines every command that DevToolsEditor can execute.
/// Lives in Assets/ (NOT in Editor/).
/// DevToolsEditor calls the static methods here via reflection-safe direct calls.
/// </summary>
public static class DevTools
{
    // ── Command Registry ──────────────────────────────────────────────────────

    public class CommandInfo
    {
        public string Name;
        public string Description;
        public string Usage;
        public System.Func<string[], string> Execute;
    }

    private static Dictionary<string, CommandInfo> _commands;

    public static Dictionary<string, CommandInfo> Commands
    {
        get
        {
            if (_commands == null) RegisterCommands();
            return _commands;
        }
    }

    private static void RegisterCommands()
    {
        _commands = new Dictionary<string, CommandInfo>(System.StringComparer.OrdinalIgnoreCase);

        Register("help",    "List all commands or describe one",       "help [command]",           CmdHelp);
        Register("get",     "Read a PlayerPref by key",                "get <key>",                CmdGet);
        Register("set",     "Write a PlayerPref (auto int/float/str)", "set <key> <value>",        CmdSet);
        Register("del",     "Delete a PlayerPref key",                 "del <key>",                CmdDel);
        Register("list",    "List all known PlayerPref keys",          "list",                     CmdList);
        Register("nuke",    "Delete ALL PlayerPrefs",                  "nuke --confirm",           CmdNuke);
        Register("streak",  "Get or set streak count",                 "streak [value]",           CmdStreak);
        Register("coins",   "Get or set coins",                        "coins [value]",            CmdCoins);
        Register("xp",      "Get or set XP",                          "xp [value]",               CmdXp);
        Register("level",   "Get or set level",                       "level [value]",            CmdLevel);
        Register("user",    "Get or set username",                     "user [name]",              CmdUser);
        Register("class",   "Get or set user class",                   "class [value]",            CmdClass);
        Register("stamp",   "Set lastStreakTimestamp N hours ago",     "stamp <hoursAgo>",         CmdStamp);
        Register("stampnow","Reset streak timestamp to right now",     "stampnow",                 CmdStampNow);
        Register("daily",   "Show or set daily challenge status",      "daily [complete|clear]",   CmdDaily);
        Register("status",  "Print full current user data snapshot",   "status",                   CmdStatus);
        Register("refresh", "Force UserDataService to reload prefs",   "refresh",                  CmdRefresh);
        Register("cls",     "Clear the console output",                "cls",                      CmdCls);
        Register("echo",    "Print a message to the console",          "echo <message>",           CmdEcho);
        Register("sim",     "Simulate streak expiry scenario",         "sim <scenario>",           CmdSim);
        Register("laststreak", "Get or set last (pre-expiry) streak count", "laststreak [value]", CmdLastStreak);
    }

    private static void Register(string name, string desc, string usage,
                                  System.Func<string[], string> fn)
    {
        _commands[name] = new CommandInfo
            { Name = name, Description = desc, Usage = usage, Execute = fn };
    }

    // ── Public execute entry ───────────────────────────────────────────────────

    public const string CLEAR_SIGNAL = "__CLEAR__";

    /// <summary>Returns the output string. CLEAR_SIGNAL means wipe the log.</summary>
    public static string Execute(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string[] parts = raw.Trim().Split(' ');
        string   cmd   = parts[0].ToLower();

        if (!Commands.TryGetValue(cmd, out var info))
            return $"<color=#FF6B6B>Unknown command: '{cmd}'. Type 'help' for a list.</color>";

        string[] args = new string[parts.Length - 1];
        System.Array.Copy(parts, 1, args, 0, args.Length);

        try   { return info.Execute(args); }
        catch (System.Exception e) { return $"<color=#FF6B6B>Error: {e.Message}</color>"; }
    }

    // ── Known pref keys (for 'list') ──────────────────────────────────────────

    private static readonly string[] KnownKeys =
    {
        "streak", "lastStreak", "coins", "xp", "currentLevel", "username", "class",
        "lastStreakTimestamp", "dailyChallengeDate", "LastPlayed",
        "InstallDate", "DaysSinceInstall", "UserIQ", "OnboardingComplete",
        "createdAt",
    };

    // ==========================================================================
    //  Command implementations
    // ==========================================================================

    private static string CmdHelp(string[] args)
    {
        if (args.Length > 0 && Commands.TryGetValue(args[0], out var info))
            return $"<color=#FFD700>{info.Name}</color> — {info.Description}\n" +
                   $"  Usage: <color=#98FB98>{info.Usage}</color>";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<color=#00BFFF>┌─────────────────────────────────────────┐</color>");
        sb.AppendLine("<color=#00BFFF>│       DMCLI — DevTools Command List      │</color>");
        sb.AppendLine("<color=#00BFFF>└─────────────────────────────────────────┘</color>");
        foreach (var kv in Commands)
        {
            sb.AppendLine(
                $"  <color=#FFD700>{kv.Value.Name,-12}</color>" +
                $"<color=#AAAAAA>{kv.Value.Description}</color>");
        }
        sb.Append("\n  Type <color=#98FB98>help <command></color> for detailed usage.");
        return sb.ToString();
    }

    private static string CmdLastStreak(string[] args) => GetOrSetInt("lastStreak", args);
    
    private static string CmdGet(string[] args)
    {
        if (args.Length == 0) return Err("Usage: get <key>");
        string key = args[0];
        if (!PlayerPrefs.HasKey(key)) return Warn($"Key '{key}' not found.");

        string s = PlayerPrefs.GetString(key, "");
        int    i = PlayerPrefs.GetInt   (key, int.MinValue);
        float  f = PlayerPrefs.GetFloat (key, float.MinValue);

        return $"<color=#00FF7F>KEY</color>  {key}\n" +
               $"  str   → <color=#FFD700>{s}</color>\n" +
               $"  int   → <color=#87CEEB>{i}</color>\n" +
               $"  float → <color=#87CEEB>{f}</color>";
    }

    private static string CmdSet(string[] args)
    {
        if (args.Length < 2) return Err("Usage: set <key> <value>");
        string key = args[0];
        string val = string.Join(" ", args, 1, args.Length - 1);

        if (int.TryParse(val, out int i))
        {
            PlayerPrefs.SetInt(key, i); PlayerPrefs.Save();
            return Ok($"Set int '{key}' = {i}");
        }
        if (float.TryParse(val, out float f))
        {
            PlayerPrefs.SetFloat(key, f); PlayerPrefs.Save();
            return Ok($"Set float '{key}' = {f}");
        }
        PlayerPrefs.SetString(key, val); PlayerPrefs.Save();
        return Ok($"Set string '{key}' = \"{val}\"");
    }

    private static string CmdDel(string[] args)
    {
        if (args.Length == 0) return Err("Usage: del <key>");
        string key = args[0];
        if (!PlayerPrefs.HasKey(key)) return Warn($"Key '{key}' doesn't exist.");
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        return Ok($"Deleted '{key}'.");
    }

    private static string CmdList(string[] args)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<color=#00BFFF>── Known PlayerPref Keys ──────────────────</color>");
        foreach (var k in KnownKeys)
        {
            bool exists = PlayerPrefs.HasKey(k);
            string val  = exists ? PlayerPrefs.GetString(k, PlayerPrefs.GetInt(k, 0).ToString()) : "<color=#555555>—</color>";
            string col  = exists ? "#FFD700" : "#555555";
            sb.AppendLine($"  <color={col}>{k,-28}</color> {val}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string CmdNuke(string[] args)
    {
        if (args.Length == 0 || args[0] != "--confirm")
            return Warn("This will wipe ALL PlayerPrefs.\nRun: <color=#98FB98>nuke --confirm</color> to proceed.");
        PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
        return "<color=#FF6B6B>⚠  ALL PlayerPrefs deleted.</color>";
    }

    private static string CmdStreak(string[] args) => GetOrSetInt("streak",       args);
    private static string CmdCoins (string[] args) => GetOrSetInt("coins",        args);
    private static string CmdXp    (string[] args) => GetOrSetInt("xp",           args);
    private static string CmdLevel (string[] args) => GetOrSetInt("currentLevel", args);
    private static string CmdUser  (string[] args) => GetOrSetStr("username",     args);
    private static string CmdClass (string[] args) => GetOrSetStr("class",        args);

    private static string CmdStamp(string[] args)
    {
        if (args.Length == 0 || !float.TryParse(args[0], out float h))
            return Err("Usage: stamp <hoursAgo>  e.g. stamp 23");

        var fakeTime = System.DateTime.Now.AddHours(-h);
        long unix    = new System.DateTimeOffset(fakeTime).ToUnixTimeSeconds();
        PlayerPrefs.SetString("lastStreakTimestamp", unix.ToString());
        PlayerPrefs.Save();

        var rem = System.TimeSpan.FromHours(24) - System.TimeSpan.FromHours(h);
        string remStr = rem.TotalSeconds > 0
            ? $"{(int)rem.TotalHours:D2}:{rem.Minutes:D2}:{rem.Seconds:D2}"
            : "EXPIRED";
        return Ok($"Timestamp set to {h}h ago  →  time remaining: <color=#FFD700>{remStr}</color>");
    }

    private static string CmdStampNow(string[] args)
    {
        long unix = new System.DateTimeOffset(System.DateTime.Now).ToUnixTimeSeconds();
        PlayerPrefs.SetString("lastStreakTimestamp", unix.ToString());
        PlayerPrefs.Save();
        return Ok("Timestamp reset to NOW — streak window is fresh (24h).");
    }

    private static string CmdDaily(string[] args)
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (args.Length == 0)
        {
            string stored = PlayerPrefs.GetString("dailyChallengeDate", "none");
            bool done = stored == today;
            return $"Daily challenge date : <color=#FFD700>{stored}</color>\n" +
                   $"Completed today      : <color={(done ? "#00FF7F" : "#FF6B6B")}>{done}</color>";
        }
        switch (args[0].ToLower())
        {
            case "complete":
                PlayerPrefs.SetString("dailyChallengeDate", today);
                PlayerPrefs.Save();
                return Ok($"Daily challenge marked complete for {today}.");
            case "clear":
                PlayerPrefs.DeleteKey("dailyChallengeDate");
                PlayerPrefs.Save();
                return Ok("Daily challenge cleared.");
            default:
                return Err("Usage: daily [complete|clear]");
        }
    }

    private static string CmdStatus(string[] args)
    {
        string ts      = PlayerPrefs.GetString("lastStreakTimestamp", "");
        string remStr  = "—";
        if (!string.IsNullOrEmpty(ts) && long.TryParse(ts, out long unix))
        {
            var reset = System.DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.AddHours(24);
            var rem   = reset - System.DateTime.Now;
            remStr    = rem.TotalSeconds > 0
                ? $"{(int)rem.TotalHours:D2}:{rem.Minutes:D2}:{rem.Seconds:D2}"
                : "<color=#FF6B6B>EXPIRED</color>";
        }

        string today    = System.DateTime.Now.ToString("yyyy-MM-dd");
        string daily    = PlayerPrefs.GetString("dailyChallengeDate", "none");
        bool   doneDay  = daily == today;

        return
            "<color=#00BFFF>┌─── User Snapshot ─────────────────────────┐</color>\n" +
            $"  username     <color=#FFD700>{PlayerPrefs.GetString("username","—")}</color>\n" +
            $"  last streak  <color=#FFD700>{PlayerPrefs.GetInt("lastStreak",0)}</color>\n" +
            $"  class        <color=#FFD700>{PlayerPrefs.GetString("class","—")}</color>\n" +
            $"  streak       <color=#FFD700>{PlayerPrefs.GetInt("streak",0)}</color>\n" +
            $"  coins        <color=#FFD700>{PlayerPrefs.GetInt("coins",0)}</color>\n" +
            $"  xp           <color=#FFD700>{PlayerPrefs.GetInt("xp",0)}</color>\n" +
            $"  level        <color=#FFD700>{PlayerPrefs.GetInt("currentLevel",0)}</color>\n" +
            $"  last stamp   <color=#87CEEB>{(string.IsNullOrEmpty(ts) ? "—" : ts)}</color>\n" +
            $"  streak rem.  <color=#98FB98>{remStr}</color>\n" +
            $"  daily done   <color={(doneDay?"#00FF7F":"#FF6B6B")}>{doneDay} ({daily})</color>\n" +
            "<color=#00BFFF>└───────────────────────────────────────────┘</color>";
    }

    private static string CmdRefresh(string[] args)
    {
#if UNITY_EDITOR
        return Warn("Refresh only works at runtime (Play mode).");
#else
        if (UserDataService.Instance == null) return Warn("UserDataService not found in scene.");
        UserDataService.Instance.FetchUserData();
        return Ok("UserDataService refreshed — UI updated.");
#endif
    }

    private static string CmdCls(string[] args)   => CLEAR_SIGNAL;
    private static string CmdEcho(string[] args)  => string.Join(" ", args);

    private static string CmdSim(string[] args)
    {
        if (args.Length == 0)
            return Warn("Scenarios: <color=#98FB98>sim dying</color> | <color=#98FB98>sim dead</color> | <color=#98FB98>sim safe</color>");

        switch (args[0].ToLower())
        {
            case "dying":  // 1.5h left
                return CmdSet(new[]{"streak","5"}) + "\n" + CmdStamp(new[]{"22.5"}) +
                       "\n" + Ok("→ Scenario: streak with 1.5h remaining (warning should trigger).");
            case "dead":   // expired
                return CmdSet(new[]{"streak","5"}) + "\n" + CmdStamp(new[]{"25"}) +
                       "\n" + Ok("→ Scenario: streak timestamp expired (25h ago).");
            case "safe":   // 20h left
                return CmdSet(new[]{"streak","5"}) + "\n" + CmdStamp(new[]{"4"}) +
                       "\n" + Ok("→ Scenario: streak safe with ~20h remaining.");
            default:
                return Err($"Unknown scenario '{args[0]}'. Try: dying | dead | safe");
        }
    }

    // ── Shared helpers ─────────────────────────────────────────────────────────

    private static string GetOrSetInt(string key, string[] args)
    {
        if (args.Length == 0)
        {
            int v = PlayerPrefs.GetInt(key, 0);
            return $"<color=#00FF7F>{key}</color> = <color=#FFD700>{v}</color>";
        }
        if (!int.TryParse(args[0], out int val)) return Err($"'{args[0]}' is not an integer.");
        PlayerPrefs.SetInt(key, val); PlayerPrefs.Save();
        return Ok($"{key} = {val}");
    }

    private static string GetOrSetStr(string key, string[] args)
    {
        if (args.Length == 0)
        {
            string v = PlayerPrefs.GetString(key, "");
            return $"<color=#00FF7F>{key}</color> = <color=#FFD700>\"{v}\"</color>";
        }
        string val = string.Join(" ", args);
        PlayerPrefs.SetString(key, val); PlayerPrefs.Save();
        return Ok($"{key} = \"{val}\"");
    }

    private static string Ok  (string msg) => $"<color=#00FF7F>✓  {msg}</color>";
    private static string Err (string msg) => $"<color=#FF6B6B>✗  {msg}</color>";
    private static string Warn(string msg) => $"<color=#FFD700>⚠  {msg}</color>";
}