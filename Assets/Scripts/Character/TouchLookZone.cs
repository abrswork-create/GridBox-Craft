using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Multiplier applied to touch dragging to control camera rotation speed")]
    [SerializeField] private float lookSensitivity = 0.2f;

    [Tooltip("If true, dragging will only be tracked if pointer starts on the right side of the screen")]
    [SerializeField] private bool restrictToRightHalf = false;

    private int activePointerId = -999;
    private Vector2 accumulatedDelta = Vector2.zero;
    private Vector2 lastPosition = Vector2.zero;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -999) return;

        if (restrictToRightHalf && eventData.position.x < Screen.width * 0.5f)
        {
            return;
        }

        activePointerId = eventData.pointerId;
        lastPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
        {
            Vector2 delta = eventData.position - lastPosition;
            accumulatedDelta += delta * lookSensitivity;
            lastPosition = eventData.position;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
        {
            activePointerId = -999;
        }
    }

    public Vector2 ConsumeLookDelta()
    {
        Vector2 delta = accumulatedDelta;
        accumulatedDelta = Vector2.zero;
        return delta;
    }
}
