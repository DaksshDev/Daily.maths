using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class score : MonoBehaviour
{
    [CoolHeader("Score")]
    
    [Space]
    [Header("Live Score Display")]
    public TMP_Text xpText;
    public TMP_Text coinsText;

    private int sessionXP;
    private int sessionCoins;
    private bool isDaily;

    public void ResetSession()
    {
        sessionXP = 0;
        sessionCoins = 0;
        UpdateDisplay();
    }

    // Call this ONCE at session start so we know if it's a daily run
    public void SetDailyMode(bool daily)
    {
        isDaily = daily;
    }

    // Called once per question attempt — small participation reward
    public void RegisterAttempt()
    {
        // Only give XP for daily attempts, coins always
        if (isDaily) sessionXP += 2;
        sessionCoins += 3;
        UpdateDisplay();
    }

    public void RegisterCorrect(float timeElapsed)
    {
        sessionXP    += 10;
        sessionCoins += 15;
        // Speed bonus removed — handled via RegisterUnbelievableBonus()
        UpdateDisplay();
    }

    public void RegisterUnbelievableBonus()
    {
        sessionXP    += 5;
        sessionCoins += 10;
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (xpText != null) xpText.text = sessionXP.ToString();
        if (coinsText != null) coinsText.text = sessionCoins.ToString();
    }

    public int GetSessionXP() => sessionXP;
    public int GetSessionCoins() => sessionCoins;
}