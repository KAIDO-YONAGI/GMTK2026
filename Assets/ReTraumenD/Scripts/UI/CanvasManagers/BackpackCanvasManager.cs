using UnityEngine;

public class BackpackCanvasManager : MonoBehaviour, ICanvasManager
{
    public CanvasGroup currentCanvas;
    public ToggleCanvasEventSO toggleBackpackCanvasEventSO;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleBackpackCanvasEventSO;
    private Canvas canvas;

    private void Awake()
    {
        canvas = currentCanvas.GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        toggleBackpackCanvasEventSO.toggleCanvasEvent += OnToggleBackpack;
    }

    private void OnDisable()
    {
        toggleBackpackCanvasEventSO.toggleCanvasEvent -= OnToggleBackpack;
    }

    private void OnToggleBackpack(bool state)
    {
        ((ICanvasManager)this).ToggleCanvas(currentCanvas, canvas, MyEnums.CanvasToToggle.Backpack, state);
    }
}
