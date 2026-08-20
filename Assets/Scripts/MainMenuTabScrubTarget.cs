using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps a main-menu tab's original click behavior while forwarding a held
/// pointer continuously, allowing the player to scrub between adjacent tabs.
/// </summary>
public sealed class MainMenuTabScrubTarget : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler
{
    private Action<PointerEventData> pointerMoved;

    public void Initialize(Action<PointerEventData> onPointerMoved)
    {
        pointerMoved = onPointerMoved;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // Tab switching should begin with the first intentional finger movement.
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerMoved?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        pointerMoved?.Invoke(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerMoved?.Invoke(eventData);
    }
}
