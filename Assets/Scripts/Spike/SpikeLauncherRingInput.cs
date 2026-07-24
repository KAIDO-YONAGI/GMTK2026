using UnityEngine;
using UnityEngine.EventSystems;

public class SpikeLauncherRingInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private SpikeLauncher spikeLauncher;

    private bool isDragging;
    private Vector2 lastPointerPosition;

    private Camera GetCamera()
    {
        return Camera.main;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (spikeLauncher == null) return;
        isDragging = true;
        lastPointerPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || spikeLauncher == null) return;

        Vector2 currentPointerPosition = eventData.position;
        Vector2 centerScreenPosition = RectTransformUtility.WorldToScreenPoint(
            GetCamera(), spikeLauncher.transform.position);

        Vector2 previousDirection = lastPointerPosition - centerScreenPosition;
        Vector2 currentDirection = currentPointerPosition - centerScreenPosition;

        if (previousDirection.sqrMagnitude < 0.001f || currentDirection.sqrMagnitude < 0.001f)
        {
            lastPointerPosition = currentPointerPosition;
            return;
        }

        float angleDelta = Vector2.SignedAngle(previousDirection, currentDirection);
        spikeLauncher.RotateByMouse(angleDelta);

        lastPointerPosition = currentPointerPosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void OnDisable()
    {
        isDragging = false;
    }
}