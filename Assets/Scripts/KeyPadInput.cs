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

    void HandleKey(string key)
    {
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