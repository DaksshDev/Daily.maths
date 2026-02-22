#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DevToolsEditor : EditorWindow
{
    // ── Constants ──────────────────────────────────────────────────────────────
    private const string MENU_PATH    = "DevTools/Console  %#d";
    private const string WINDOW_TITLE = "DMCLI — DevTools Console";
    private const string PROMPT       = "Dev/Debug/dmcli.exe >> ";
    private const string INPUT_CTRL   = "dmcli_input";
    private const int    FONT_SIZE    = 13;
    private const int    MAX_LINES    = 1500;

    private static readonly Color BG_COLOR     = Color.black;
    private static readonly Color TOOLBAR_BG   = new Color(0.08f, 0.08f, 0.08f);
    private static readonly Color INPUT_ROW_BG = new Color(0.04f, 0.04f, 0.04f);
    private static readonly Color SEP_COL      = new Color(0.18f, 0.18f, 0.18f);
    private static readonly Color TEXT_COL     = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color PROMPT_COL   = new Color(0.28f, 0.72f, 1f);

    // ── State ──────────────────────────────────────────────────────────────────
    private readonly List<string> _lines   = new List<string>();
    private string   _rawLog   = "";
    private string   _input    = "";
    private Vector2  _scroll;
    private bool     _dirty        = true;
    private bool     _scrollBottom = true;
    private bool     _wantFocus    = true;
    private readonly List<string> _history = new List<string>();
    private int      _histIdx = -1;

    // ── Styles ─────────────────────────────────────────────────────────────────
    private GUIStyle  _sOutput;
    private GUIStyle  _sInput;
    private GUIStyle  _sPrompt;
    private GUIStyle  _sToolBtn;
    private bool      _stylesReady;

    // ── Open ───────────────────────────────────────────────────────────────────
    [MenuItem(MENU_PATH)]
    public static void Open()
    {
        var w = GetWindow<DevToolsEditor>(false, WINDOW_TITLE, true);
        w.minSize = new Vector2(620, 400);
    }

    private void OnEnable()  { _stylesReady = false; PrintBanner(); }
    private void OnFocus()   { _wantFocus = true; Repaint(); }

    // ── OnGUI ──────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        // Build styles lazily (must happen inside OnGUI on first call)
        if (!_stylesReady) BuildStyles();

        var e = Event.current;

        // ── Intercept Enter BEFORE any control consumes it ─────────────────────
        // We do this at the very top so nothing else can swallow the event.
        if (e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            Submit();
            e.Use();
            return; // skip rest of layout this frame — Repaint queued by Submit
        }

        // ── Other hotkeys ──────────────────────────────────────────────────────
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.UpArrow)   { HistoryNav(+1); e.Use(); }
            else if (e.keyCode == KeyCode.DownArrow) { HistoryNav(-1); e.Use(); }
            else if (e.keyCode == KeyCode.Tab)   { TabComplete();  e.Use(); }
            else if (e.keyCode == KeyCode.L && e.control) { RunCmd("cls"); e.Use(); }
        }

        // ── Layout ────────────────────────────────────────────────────────────
        float toolbarH  = 26f;
        float inputRowH = FONT_SIZE + 16f;
        float outputH   = position.height - toolbarH - inputRowH - 1f;

        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BG_COLOR);

        DrawToolbar (new Rect(0,                        0, position.width, toolbarH));
        DrawOutput  (new Rect(0,               toolbarH,  position.width, outputH));
        EditorGUI.DrawRect(new Rect(0, toolbarH + outputH, position.width, 1f), SEP_COL);
        DrawInputRow(new Rect(0, toolbarH + outputH + 1f,  position.width, inputRowH));
    }

    // ── Toolbar ────────────────────────────────────────────────────────────────
    private void DrawToolbar(Rect r)
    {
        EditorGUI.DrawRect(r, TOOLBAR_BG);
        GUILayout.BeginArea(r);
        GUILayout.BeginHorizontal();
        GUILayout.Space(4);
        if (TBtn("Clear",      46)) RunCmd("cls");
        if (TBtn("Status",     52)) RunCmd("status");
        if (TBtn("List",       40)) RunCmd("list");
        if (TBtn("Nuke ⚠",    58))
        {
            if (EditorUtility.DisplayDialog("Confirm Nuke",
                    "Delete ALL PlayerPrefs?", "Nuke it", "Cancel"))
                RunCmd("nuke --confirm");
        }
        GUILayout.Space(8);
        if (TBtn("Sim: Dying", 76)) RunCmd("sim dying");
        if (TBtn("Sim: Dead",  70)) RunCmd("sim dead");
        if (TBtn("Sim: Safe",  68)) RunCmd("sim safe");
        GUILayout.FlexibleSpace();
        bool play  = EditorApplication.isPlaying;
        var  prev  = GUI.color;
        GUI.color  = play ? new Color(0f,1f,0.44f) : new Color(1f,0.42f,0.42f);
        GUILayout.Label(play ? "● PLAY" : "■ EDIT", _sPrompt, GUILayout.Width(55));
        GUI.color  = prev;
        GUILayout.Space(6);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private bool TBtn(string label, float w) =>
        GUI.Button(GUILayoutUtility.GetRect(w, 22,
            GUILayout.Width(w), GUILayout.Height(22)), label, _sToolBtn);

    // ── Output ─────────────────────────────────────────────────────────────────
    private void DrawOutput(Rect r)
    {
        if (_dirty)
        {
            var sb = new System.Text.StringBuilder(_lines.Count * 60);
            foreach (var l in _lines) { sb.Append(l); sb.Append('\n'); }
            _rawLog = sb.ToString();
            _dirty  = false;
        }

        float contentH = Mathf.Max(r.height,
            _sOutput.CalcHeight(new GUIContent(_rawLog), r.width - 16));

        _scroll = GUI.BeginScrollView(r, _scroll,
            new Rect(0, 0, r.width - 16, contentH),
            false, false);

        GUI.Label(new Rect(0, 0, r.width - 16, contentH), _rawLog, _sOutput);
        GUI.EndScrollView();

        if (_scrollBottom) { _scroll.y = float.MaxValue; _scrollBottom = false; Repaint(); }
    }

    // ── Input row ──────────────────────────────────────────────────────────────
    private void DrawInputRow(Rect r)
    {
        EditorGUI.DrawRect(r, INPUT_ROW_BG);

        float padY    = Mathf.Floor((r.height - FONT_SIZE - 2f) * 0.5f);
        float promptW = _sPrompt.CalcSize(new GUIContent(PROMPT)).x;

        // Prompt label
        GUI.Label(new Rect(r.x + 6, r.y + padY, promptW + 4, r.height), PROMPT, _sPrompt);

        // Input field — invisible box, no chrome
        Rect fieldR = new Rect(r.x + 6 + promptW, r.y + padY,
                               r.width - promptW - 12, FONT_SIZE + 4f);

        GUI.SetNextControlName(INPUT_CTRL);
        _input = GUI.TextField(fieldR, _input, _sInput);

        if (_wantFocus)
        {
            GUI.FocusControl(INPUT_CTRL);
            _wantFocus = false;
        }
    }

    // ── Submit ─────────────────────────────────────────────────────────────────
    private void Submit()
    {
        string cmd = _input.Trim();
        _input     = "";
        _wantFocus = true;

        string promptColHex = "47B8FF";
        Push($"<color=#{promptColHex}>{PROMPT}</color><color=#FFFFFF>{EscRich(cmd)}</color>");

        if (!string.IsNullOrEmpty(cmd))
        {
            if (_history.Count == 0 || _history[0] != cmd) _history.Insert(0, cmd);
            if (_history.Count > 100) _history.RemoveAt(_history.Count - 1);
            _histIdx = -1;

            string result = DevTools.Execute(cmd);
            if (result == DevTools.CLEAR_SIGNAL)
            {
                _lines.Clear(); _dirty = true;
            }
            else if (!string.IsNullOrEmpty(result))
            {
                foreach (var line in result.Split('\n')) Push(line);
            }
        }

        Push("");
        _scrollBottom = true;
        Repaint();
    }

    private void RunCmd(string cmd) { _input = cmd; Submit(); }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private void Push(string rich)
    {
        _lines.Add(rich);
        if (_lines.Count > MAX_LINES) _lines.RemoveAt(0);
        _dirty = true;
    }

    private void HistoryNav(int dir)
    {
        if (_history.Count == 0) return;
        _histIdx = Mathf.Clamp(_histIdx + dir, -1, _history.Count - 1);
        _input   = _histIdx >= 0 ? _history[_histIdx] : "";
        Repaint();
    }

    private void TabComplete()
    {
        if (string.IsNullOrEmpty(_input)) return;
        foreach (var kv in DevTools.Commands)
            if (kv.Key.StartsWith(_input, System.StringComparison.OrdinalIgnoreCase))
            { _input = kv.Key; Repaint(); break; }
    }

    private static string EscRich(string s) => s.Replace("<","‹").Replace(">","›");

    // ── Banner ─────────────────────────────────────────────────────────────────
    private void PrintBanner()
    {
        _lines.Clear();
        const string b = "#00BFFF";
        Push($"<color={b}>  ██████╗ ███╗   ███╗  ██████╗██╗     ██╗</color>");
        Push($"<color={b}>  ██╔══██╗████╗ ████║ ██╔════╝██║     ██║</color>");
        Push($"<color={b}>  ██║  ██║██╔████╔██║ ██║     ██║     ██║</color>");
        Push($"<color={b}>  ██║  ██║██║╚██╔╝██║ ██║     ██║     ██║</color>");
        Push($"<color={b}>  ██████╔╝██║ ╚═╝ ██║ ╚██████╗███████╗██║</color>");
        Push($"<color={b}>  ╚═════╝ ╚═╝     ╚═╝  ╚═════╝╚══════╝╚═╝</color>");
        Push("");
        Push("<color=#AAAAAA>  DevTools Management Console  v1.0</color>");
        Push($"<color=#444444>  Unity {Application.unityVersion}  |  {System.DateTime.Now:yyyy-MM-dd HH:mm}</color>");
        Push("<color=#333333>  ─────────────────────────────────────────────────────</color>");
        Push("  Type <color=#98FB98>help</color> for commands   " +
             "<color=#555555>|</color>  <color=#98FB98>Tab</color> autocomplete   " +
             "<color=#555555>|</color>  <color=#98FB98>↑↓</color> history   " +
             "<color=#555555>|</color>  <color=#98FB98>Ctrl+L</color> clear");
        Push("");
        _dirty        = true;
        _scrollBottom = true;
    }

    // ── Style builder ──────────────────────────────────────────────────────────
    private void BuildStyles()
    {
        _stylesReady = true;
        var mono     = LoadMonoFont();

        _sOutput = new GUIStyle(GUIStyle.none)
        {
            richText  = true,
            wordWrap  = false,
            padding   = new RectOffset(8, 8, 6, 6),
            normal    = { textColor = TEXT_COL },
        };
        if (mono != null) { _sOutput.font = mono; _sOutput.fontSize = FONT_SIZE; }

        var clearTex = MakeTex(new Color(0,0,0,0));
        _sInput = new GUIStyle(GUIStyle.none)
        {
            richText  = false,
            clipping  = TextClipping.Clip,
            padding   = new RectOffset(0,0,0,0),
            border    = new RectOffset(0,0,0,0),
            normal    = { textColor = Color.white, background = clearTex },
            focused   = { textColor = Color.white, background = clearTex },
            active    = { textColor = Color.white, background = clearTex },
            hover     = { textColor = Color.white, background = clearTex },
        };
        if (mono != null) { _sInput.font = mono; _sInput.fontSize = FONT_SIZE; }

        _sPrompt = new GUIStyle(GUIStyle.none)
        {
            richText  = false,
            padding   = new RectOffset(0,0,0,0),
            normal    = { textColor = PROMPT_COL },
        };
        if (mono != null) { _sPrompt.font = mono; _sPrompt.fontSize = FONT_SIZE; }

        _sToolBtn = new GUIStyle(EditorStyles.miniButton) { fontSize = 11 };
    }

    // Avoids Font.CreateDynamicFontFromOSFont which can trigger the
    // "GameObject name cannot be empty" error in some Unity versions.
    private static Font LoadMonoFont()
    {
        // 1. Try loading a bundled font asset from the project first
        var asset = Resources.Load<Font>("Fonts/RobotoMono-Regular");
        if (asset != null) return asset;

        // 2. Scan OS fonts by name safely
        string[] candidates = { "Courier New", "Consolas", "Lucida Console", "Courier", "Monaco" };
        var      available  = new System.Collections.Generic.HashSet<string>(
                                  Font.GetOSInstalledFontNames(),
                                  System.StringComparer.OrdinalIgnoreCase);

        foreach (var name in candidates)
        {
            if (!available.Contains(name)) continue;
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 13);
                if (f != null) return f;
            }
            catch { /* skip */ }
        }

        return null; // fall back to Unity default — no crash
    }

    private static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }
}
#endif