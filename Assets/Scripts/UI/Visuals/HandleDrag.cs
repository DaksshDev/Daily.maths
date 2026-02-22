using UnityEngine;
using UnityEngine.EventSystems;

public class HandleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [CoolHeader("Drag Handler")]
    
    [Space]
    public Drawer drawer;   // Assign in inspector

    public void OnBeginDrag(PointerEventData eventData)
    {
        drawer.StartDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        drawer.Drag(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        drawer.EndDrag();
    }
}