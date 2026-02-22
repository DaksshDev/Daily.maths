using UnityEngine;
using System.Collections;
public class Drawer : MonoBehaviour
{
    [CoolHeader("-DRAWER-")]
    
    [Space]
    public RectTransform drawerPanel;

    [Header("Snap Settings")]
    public Vector2[] snapPositions;   // Allowed positions
    public float snapThreshold = 50f; // How close it needs to be
    public float snapSpeed = 10f;     // Smoothness
    
    [Header("Limits")]
    public float minY = 0f;
    public float maxY = 600f;

    private float initialX;  // Store only X, let Y be dynamic
    private Vector2 targetPosition;
    private bool snapping = false;
    private bool initialized = false;

    void Awake()
    {
        Initialize();
    }

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        
        if (drawerPanel == null)
        {
            return;
        }
        
        initialX = drawerPanel.anchoredPosition.x;
        targetPosition = drawerPanel.anchoredPosition;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) Initialize();
        
        if (snapping)
        {
            drawerPanel.anchoredPosition = Vector2.Lerp(
                drawerPanel.anchoredPosition,
                targetPosition,
                Time.deltaTime * snapSpeed
            );

            if (Vector2.Distance(drawerPanel.anchoredPosition, targetPosition) < 0.5f)
            {
                drawerPanel.anchoredPosition = targetPosition;
                snapping = false;
                Debug.Log($"✅ Drawer snapped to: {targetPosition}");
            }
        }
    }

    public void Drag(Vector2 delta)
    {
        if (!initialized) Initialize();
        
        float newY = drawerPanel.anchoredPosition.y + delta.y;
        newY = Mathf.Clamp(newY, minY, maxY);
        drawerPanel.anchoredPosition = new Vector2(initialX, newY);
    }

    public void EndDrag()
    {
        if (!initialized) Initialize();
        
        float currentY = drawerPanel.anchoredPosition.y;
        System.Array.Sort(snapPositions, (a, b) => a.y.CompareTo(b.y));

        float chosenY = snapPositions[0].y;

        for (int i = 0; i < snapPositions.Length - 1; i++)
        {
            float lower = snapPositions[i].y;
            float upper = snapPositions[i + 1].y;
            float midpoint = (lower + upper) / 2f;

            if (currentY < midpoint)
            {
                chosenY = lower;
                break;
            }
            else
            {
                chosenY = upper;
            }
        }

        targetPosition = new Vector2(initialX, chosenY);
        snapping = true;
    }

    public void StartDrag()
    {
        snapping = false;
    }

    public void SetDrawerPosition(float yPos)
    {
        if (!initialized) Initialize();
        
        targetPosition = new Vector2(initialX, yPos);
        snapping = true;
    }

    public void CloseDrawer()
    {
        if (snapPositions == null || snapPositions.Length == 0)
        {
            SetDrawerPosition(-800f);
        }
        else
        {
            // Snap to lowest position (index 0 after sort), then deactivate
            System.Array.Sort(snapPositions, (a, b) => a.y.CompareTo(b.y));
            SetDrawerPosition(snapPositions[0].y);
        }

        StartCoroutine(CloseDrawerCoroutine());
    }

    IEnumerator CloseDrawerCoroutine()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
    }
    
    // Alternative: instantly set position without animation
    public void SetDrawerPositionImmediate(float yPos)
    {
        if (!initialized) Initialize();
    
        snapping = false;
    
        // Force Unity to finish any pending layout calculations first
        Canvas.ForceUpdateCanvases();
    
        drawerPanel.anchoredPosition = new Vector2(initialX, yPos);
        targetPosition = drawerPanel.anchoredPosition;
    }
}