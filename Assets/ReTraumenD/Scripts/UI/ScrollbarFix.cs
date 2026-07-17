// 修复 Unity ScrollRect 与 Scrollbar 双向同步导致的回弹/鬼畜问题
// 原因：ScrollRect.LateUpdate 每帧根据 content bounds 重算 normalizedPosition 并写回 Scrollbar，
// 而 Scrollbar 拖拽时也在反向修改 normalizedPosition，形成循环覆盖。
// 方案：运行时断开 ScrollRect 的 Scrollbar 引用，改为手动单向同步，
// 拖拽期间暂停反向同步（skipFrames），等 ScrollRect 内部稳定后再恢复。
// 用法：挂到 Scrollbar 对象上即可，自动查找 ScrollRect。

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Scrollbar))]
public class ScrollbarFix : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private ScrollRect scrollRect;
    private Scrollbar scrollbar;
    private int skipFrames;

    private void Start()
    {
        scrollRect = GetComponentInParent<ScrollRect>();
        scrollbar = GetComponent<Scrollbar>();

        scrollRect.verticalScrollbar = null;
        scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        StartCoroutine(InitializeScrollPosition());
    }

    private void OnScrollbarChanged(float value)
    {
        if (skipFrames > 0)
            scrollRect.verticalNormalizedPosition = value;
    }

    private void LateUpdate()
    {
        if (!CanScrollVertically())
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
            scrollbar.SetValueWithoutNotify(1f);
            return;
        }

        if (skipFrames > 0)
            skipFrames--;
        else
            scrollbar.SetValueWithoutNotify(scrollRect.verticalNormalizedPosition);
    }

    public void OnDrag(PointerEventData eventData) => skipFrames = 2;
    public void OnPointerDown(PointerEventData eventData) => skipFrames = 2;
    public void OnPointerUp(PointerEventData eventData) => skipFrames = 2;

    private IEnumerator InitializeScrollPosition()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
        scrollbar.SetValueWithoutNotify(1f);
    }

    private bool CanScrollVertically()
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            return false;

        return scrollRect.content.rect.height > scrollRect.viewport.rect.height + 0.01f;
    }

    private void OnDestroy()
    {
        if (scrollbar != null)
            scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
    }
}
