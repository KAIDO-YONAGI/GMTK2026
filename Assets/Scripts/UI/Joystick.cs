using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform backgroundRect;
    public RectTransform handleRect;

    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    private Vector2 inputVector;
    private float handleRange = 1f;

    private void Awake()
    {
#if !UNITY_ANDROID && !UNITY_IOS && !UNITY_WEBGL
        gameObject.SetActive(false);
#endif
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position = RectTransformUtility.WorldToScreenPoint(null, backgroundRect.position);
        Vector2 radius = new Vector2(backgroundRect.rect.width / 2, backgroundRect.rect.height / 2);
        inputVector = (eventData.position - position) / radius;

        if (inputVector.magnitude > 1f)
            inputVector = inputVector.normalized;

        handleRect.anchoredPosition = inputVector * radius * handleRange;

        Horizontal = inputVector.x;
        Vertical = inputVector.y;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
        Horizontal = 0f;
        Vertical = 0f;
    }
}
