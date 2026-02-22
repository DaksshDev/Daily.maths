using System;
using UnityEngine;
using System.Collections;

public class WelcomeDrawSeq : MonoBehaviour
{
    [SettingsHeader("FirstUse Welcome Handler")]
    
    [Space]
    public Drawer drawer;
    public float positionThreshold = 10f;
    public PullToRefresh pullToRefresh; // Reference to PullToRefresh script

    public void WelcomeDraw()
    {
        StartCoroutine(Welcome());
    }

    public IEnumerator SetupDrawerInClosedPosition()
    {
        if (drawer == null)
        {
            yield break;
        }
        drawer.gameObject.SetActive(true);
        drawer.SetDrawerPositionImmediate(-1700);
        yield return new WaitForEndOfFrame();
        drawer.gameObject.SetActive(false);
    }

    IEnumerator Welcome()
    {
        // Disable pull to refresh spinner during welcome animation
        if (pullToRefresh != null)
        {
            pullToRefresh.DisableSpinnerDuringWelcome();
        }

        yield return new WaitForSeconds(1f);
        drawer.gameObject.SetActive(true);
        yield return new WaitForEndOfFrame();
        drawer.SetDrawerPosition(-232);
        
        // Wait a bit for drawer animation to complete, then re-enable spinner logic
        yield return new WaitForSeconds(0.5f);
        
        if (pullToRefresh != null)
        {
            pullToRefresh.EnableSpinnerAfterWelcome();
        }
    }
}