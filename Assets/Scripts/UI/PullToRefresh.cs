using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PullToRefresh : MonoBehaviour
{
    [SettingsHeader("Pull To Refresh Handler")]
    
    [Space]
    [Header("References")]
    public RectTransform drawer;
    public GameObject spinner;
    public GameObject refreshText; // NEW: Reference to refresh text
    
    [Header("Position Range")]
    private float minY = -500f;
    private float maxY = -300;
    
    [Header("Spinner Activation")]
    private float spinnerActivationThreshold = -232.1116f;
    
    [Header("Rotation Range")]
    private float maxRotation = 192f;
    private float minRotation = 0f;
    
    [Header("Color Settings")]
    private float colorChangeStart = -400f;
    private float colorChangeEnd = -500f;
    
    [Header("Smoothing")]
    public float rotationSmoothSpeed = 10f;
    public float colorSmoothSpeed = 5f;
    
    private Image spinnerImage;
    private float targetRotation;
    private Color targetColor;
    private Color whiteColor = Color.white;
    private Color blackColor = Color.black;
    
    private float previousY;
    private bool wasAtBottom = false;
    private bool isWelcomeAnimationRunning = false;
    
    void Start()
    {
        if (spinner != null)
        {
            spinnerImage = spinner.GetComponent<Image>();
            if (spinnerImage != null)
            {
                spinnerImage.color = whiteColor;
            }
            spinner.SetActive(false);
        }
        
        // Hide refresh text initially
        if (refreshText != null)
        {
            refreshText.SetActive(false);
        }
        
        if (drawer != null)
        {
            previousY = drawer.anchoredPosition.y;
        }
    }
    
    void Update()
    {
        if (drawer == null || spinner == null || spinnerImage == null) return;
        
        float currentY = drawer.anchoredPosition.y;
        
        // Show/Hide spinner and refresh text based on threshold
        // But ALWAYS hide if welcome animation is running
        if (isWelcomeAnimationRunning)
        {
            if (spinner.activeSelf)
            {
                spinner.SetActive(false);
            }
            if (refreshText != null && refreshText.activeSelf)
            {
                refreshText.SetActive(false);
            }
        }
        else if (currentY < spinnerActivationThreshold)
        {
            if (!spinner.activeSelf)
            {
                spinner.SetActive(true);
            }
            if (refreshText != null && !refreshText.activeSelf)
            {
                refreshText.SetActive(true);
            }
        }
        else
        {
            if (spinner.activeSelf)
            {
                spinner.SetActive(false);
            }
            if (refreshText != null && refreshText.activeSelf)
            {
                refreshText.SetActive(false);
            }
        }
        
        // Calculate target rotation based on drawer Y position
        if (currentY >= minY && currentY <= maxY)
        {
            float t = Mathf.InverseLerp(minY, maxY, currentY);
            targetRotation = Mathf.Lerp(maxRotation, minRotation, t);
        }
        else if (currentY < minY)
        {
            targetRotation = maxRotation;
        }
        else if (currentY > maxY)
        {
            targetRotation = minRotation;
        }
        
        // Smoothly rotate spinner
        Vector3 currentRot = spinner.transform.localEulerAngles;
        float smoothedRotation = Mathf.LerpAngle(currentRot.z, targetRotation, Time.deltaTime * rotationSmoothSpeed);
        spinner.transform.localEulerAngles = new Vector3(currentRot.x, currentRot.y, smoothedRotation);
        
        // Calculate target color ONLY between -400 and -500
        if (currentY <= colorChangeStart && currentY >= colorChangeEnd)
        {
            float colorT = Mathf.InverseLerp(colorChangeStart, colorChangeEnd, currentY);
            targetColor = Color.Lerp(whiteColor, blackColor, colorT);
        }
        else if (currentY > colorChangeStart)
        {
            targetColor = whiteColor;
        }
        else if (currentY < colorChangeEnd)
        {
            targetColor = blackColor;
        }
        
        // Smoothly change color
        spinnerImage.color = Color.Lerp(spinnerImage.color, targetColor, Time.deltaTime * colorSmoothSpeed);
        
        // Detect if drawer was released from bottom position
        if (currentY <= -500f)
        {
            wasAtBottom = true;
        }
        
        // Check if drawer is moving back up after being at bottom
        if (wasAtBottom && currentY > previousY && currentY >= maxY - 10f)
        {
            RefreshScene();
            wasAtBottom = false;
        }
        
        previousY = currentY;
    }
    
    // Public methods to control spinner and text during welcome animation
    public void DisableSpinnerDuringWelcome()
    {
        isWelcomeAnimationRunning = true;
        if (spinner != null)
        {
            spinner.SetActive(false);
        }
        if (refreshText != null)
        {
            refreshText.SetActive(false);
        }
    }
    
    public void EnableSpinnerAfterWelcome()
    {
        isWelcomeAnimationRunning = false;
    }
    
    void RefreshScene()
    {
        //TOADD: Add your reload/refresh logic here
    }
}