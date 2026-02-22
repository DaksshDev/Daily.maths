using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ConnectionCheck : MonoBehaviour
{
    [CoolHeader("Internet Connection Check")]
    
    [Space]
    [Header("UI References")]
    public GameObject noInternetMessageObject;
    public GameObject noInternetIconObject;

    [Header("Settings")]
    public float checkInterval = 3f;

    private bool isOffline = false;
    private float timer = 0f;

    void Start()
    {
        // Hide both UI elements at start
        noInternetMessageObject.SetActive(false);
        noInternetIconObject.SetActive(false);

        // Do an initial check right away
        StartCoroutine(CheckConnection());
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= checkInterval)
        {
            timer = 0f;
            StartCoroutine(CheckConnection());
        }
    }

    private IEnumerator CheckConnection()
    {
        // Ping a reliable server (Google DNS)
        UnityWebRequest request = new UnityWebRequest("https://www.google.com");
        request.timeout = 5;

        yield return request.SendWebRequest();

        bool hasConnection = request.result == UnityWebRequest.Result.Success ||
                             request.result == UnityWebRequest.Result.ProtocolError;
        // ProtocolError = server replied = internet works

        if (!hasConnection && !isOffline)
        {
            // Just went offline
            isOffline = true;
            noInternetMessageObject.SetActive(true);
            noInternetIconObject.SetActive(true);
            Debug.Log("Connection lost!");
        }
        else if (hasConnection && isOffline)
        {
            // Just came back online
            isOffline = false;
            noInternetMessageObject.SetActive(false);
            noInternetIconObject.SetActive(false);
            Debug.Log("Connection restored!");
        }

        request.Dispose();
    }
}