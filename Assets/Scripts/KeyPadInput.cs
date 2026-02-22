using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeyPadInput : MonoBehaviour
{
    [CoolHeader("Keypad")]
    
    [Space]
    [Header("References")]
    public GameObject keypadParent;
    public TMP_Text displayText;

    // When locked, all key presses are silently ignored.
    // GameManager locks the pad when the timer hits 0 and unlocks it
    // when the next question is fully loaded.
    private bool _locked = false;

    void Start()
    {
        string[] keyNames = { "1","2","3","4","5","6","7","8","9","0","delete","upon","point","negative" };

        foreach (string keyName in keyNames)
        {
            Transform keyTransform = keypadParent.transform.Find(keyName);
            if (keyTransform == null) continue;

            Button btn = keyTransform.GetComponent<Button>();
            if (btn == null) continue;

            string captured = keyName;
            btn.onClick.AddListener(() => HandleKey(captured));
        }
    }

    // ── Lock control ──────────────────────────────────────────────────────────

    /// <summary>Lock or unlock the keypad. While locked, input is ignored.</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
    }

    /// <summary>
    /// Immediately wipe the display text regardless of lock state.
    /// Called by AnswerChecker.ForceHardClearInput() on timer expiry.
    /// </summary>
    public void ForceClearDisplay()
    {
        if (displayText != null)
            displayText.text = "";
    }

    // ── Key handling ──────────────────────────────────────────────────────────

    void HandleKey(string key)
    {
        if (_locked) return;  // ← drop late taps silently

        switch (key)
        {
            case "delete":
                if (displayText.text.Length > 0)
                    displayText.text = displayText.text[..^1];
                break;

            case "upon":
                displayText.text += "/";
                break;

            case "point":
                displayText.text += ".";
                break;

            case "negative":
                displayText.text += "-";
                break;

            default:
                displayText.text += key;
                break;
        }
    }
}