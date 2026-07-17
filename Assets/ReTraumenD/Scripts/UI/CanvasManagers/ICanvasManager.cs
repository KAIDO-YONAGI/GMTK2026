using UnityEngine;

public interface ICanvasManager
{
    ToggleCanvasEventSO ToggleCanvasEvent { get; }
    public void ToggleCanvas(
        CanvasGroup canvasGroup,
        Canvas canvas,
        MyEnums.CanvasToToggle canvasToToggle,
        bool state)//统一调用两个函数的接口函数，用于回调画布状态和真正设置优先级 
    {
        SetCanvaState(canvasGroup, canvasToToggle, state);
        RefreshCanvaOrder(canvas, canvasToToggle, state);
        // Debug.Log("toggle&order");
    }
    void SetCanvaState(
          CanvasGroup canva,
          MyEnums.CanvasToToggle canvasToToggle,
          bool state)//如果操作成功了，那就告知manager
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;

        if (UIManager.instance != null)
        {
            UIManager.instance.ReportCanvasState(canvasToToggle, state);
        }
    }

    void RefreshCanvaOrder(
       Canvas canvas,
       MyEnums.CanvasToToggle canvasToToggle,
       bool state)
    {
        int order = state && UIManager.instance != null &&
                    UIManager.instance.IsCanvasFocused(canvasToToggle)
            //如果Manager里当前已经focus了该画布，就设置当前画布的为聚焦顺序，否则为默认顺序
            ? UIManager.FocusOrder
            : UIManager.DefaultOrder;
        if (canvas == null)
        {
            return;
        }

        canvas.sortingOrder = order;
    }



}
