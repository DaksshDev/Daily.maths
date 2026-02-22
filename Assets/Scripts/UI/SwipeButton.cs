using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class SwipeButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [CoolHeader("Swipe Button")] 
    
    [Space]
    [Header("Panel References")]
    public RectTransform draggablePanel;
    public CanvasGroup visualPanel;
    public RectTransform buttonContainer;

    [Header("Positions")]
    public float[] xPositions = new float[] { -600f, 0f };

    [Header("Settings")]
    public float snapThreshold = 100f;
    public float smoothSpeed = 10f;
    public float scaleMultiplier = 1.15f;

    [Header("Events")]
    public UnityEvent Triggers;

    private Vector2 lastPos;
    private bool isDragging;
    private int targetIndex = 0;
    private bool wasTriggered = false;

    void Start()
    {
        SetPanelPosition(xPositions[0]);
        UpdateVisualPanel(0f);
        UpdateButtonScale(0f);
    }

    void Update()
    {
        if (!isDragging)
        {
            float currentX = draggablePanel.anchoredPosition.x;
            float targetX = xPositions[targetIndex];
            float newX = Mathf.Lerp(currentX, targetX, Time.deltaTime * smoothSpeed);
            
            SetPanelPosition(newX);
            
            float normalizedPos = Mathf.InverseLerp(xPositions[0], xPositions[1], newX);
            UpdateVisualPanel(normalizedPos);
            UpdateButtonScale(normalizedPos);

            if (targetIndex == 1 && !wasTriggered)
            {
                Triggers?.Invoke();
                wasTriggered = true;
            }
            else if (targetIndex == 0)
            {
                wasTriggered = false;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        lastPos = draggablePanel.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 currentPos = draggablePanel.anchoredPosition;
        currentPos.x += eventData.delta.x;
        currentPos.x = Mathf.Clamp(currentPos.x, xPositions[0], xPositions[1]);
        
        draggablePanel.anchoredPosition = currentPos;
        
        float normalizedPos = Mathf.InverseLerp(xPositions[0], xPositions[1], currentPos.x);
        UpdateVisualPanel(normalizedPos);
        UpdateButtonScale(normalizedPos);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        float currentX = draggablePanel.anchoredPosition.x;
        
        targetIndex = (Mathf.Abs(currentX - xPositions[1]) < snapThreshold) ? 1 : 0;
    }

    void SetPanelPosition(float x)
    {
        Vector2 pos = draggablePanel.anchoredPosition;
        pos.x = x;
        draggablePanel.anchoredPosition = pos;
    }

    void UpdateVisualPanel(float alpha)
    {
        visualPanel.alpha = alpha;
        visualPanel.interactable = alpha > 0.5f;
        visualPanel.blocksRaycasts = alpha > 0.5f;
    }

    void UpdateButtonScale(float t)
    {
        float scale = Mathf.Lerp(1f, scaleMultiplier, t);
        buttonContainer.localScale = Vector3.one * scale;
    }
    
    public void ResetPosToOff()
    {
        targetIndex = 0;
        isDragging = false;
        wasTriggered = false;
        SetPanelPosition(xPositions[0]);
        UpdateVisualPanel(0f);
        UpdateButtonScale(0f);
    }
}