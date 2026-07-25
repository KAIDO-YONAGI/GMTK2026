using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpikeLauncherRingInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private SpikeLauncher spikeLauncher;

    [Header("Disable State")]
    public bool disableRotation;
    public Color disabledColor = Color.gray;

    private bool isDragging;
    private Vector2 lastPointerPosition;
    private Graphic _graphic;
    private Color _originalColor;

    private void Awake()
    {
        CacheGraphic();
        ApplyState();
    }

    private void OnValidate()
    {
        CacheGraphic();
        ApplyState();
    }

    private void CacheGraphic()
    {
        if (_graphic != null) return;
        _graphic = GetComponent<Graphic>();
        if (_graphic != null)
            _originalColor = _graphic.color;
    }

    private void ApplyState()
    {
        if (_graphic == null) return;
        _graphic.color = disableRotation ? disabledColor : _originalColor;
    }

    public void Set()
    {
        disableRotation = true;
        ApplyState();
    }

    public void Unset()
    {
        disableRotation = false;
        ApplyState();
    }

    public void Revert()
    {
        disableRotation = !disableRotation;
        ApplyState();
    }

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
        if (disableRotation) return;

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
