using UnityEngine;
using UnityEngine.UI;

public class ESCButton : MonoBehaviour
{
    [Tooltip("点击后请求关闭指定面板的按钮。")]
    public Button escButton;

    [Tooltip("该按钮要关闭的面板类型。显式关闭不受 Close On Escape 限制。")]
    [SerializeField] private MyEnums.CanvasToToggle canvasToESC;

    private void OnEnable()
    {
        if (escButton != null)
            escButton.onClick.AddListener(OnESC);
    }

    private void OnDisable()
    {
        if (escButton != null)
            escButton.onClick.RemoveListener(OnESC);
    }

    private void OnESC()
    {
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.RequestCanvasClose(canvasToESC);
    }
}
