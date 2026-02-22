using UnityEngine;
using TMPro;

public class UserGreet : MonoBehaviour
{
    [CoolHeader("User Greet")]
    
    [Space]
    public TMP_Text greetingText;

    public void SetUsername(string username)
    {
        if (greetingText != null)
        {
            greetingText.text = "Hello, " + username;
        }
    }
}