using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 回溯导引 Canvas：监听两个 VoidEventSO（show/exit）控制显隐。
/// 显示期间用协程在两帧精灵间循环切换，模拟 R 键按下动画。
/// </summary>
public class RewindGuideCanvas : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("触发后显示导引。")]
    [SerializeField] private VoidEventSO showEvent;

    [Tooltip("触发后隐藏导引。")]
    [SerializeField] private VoidEventSO exitEvent;

    [Header("Animation")]
    [Tooltip("R 键动画的 Image 组件（显示精灵用）。")]
    [SerializeField] private Image keyImage;

    [Tooltip("第 0 帧（未按下）。拖入 R_0 子精灵。")]
    [SerializeField] private Sprite frameUp;

    [Tooltip("第 1 帧（按下）。拖入 R_1 子精灵。")]
    [SerializeField] private Sprite frameDown;

    [Tooltip("帧切换间隔（秒）。0.125 = 8 FPS。")]
    [SerializeField] private float frameInterval = 0.125f;

    // 动画协程句柄
    private Coroutine animRoutine;

    private void OnEnable()
    {
        if (showEvent != null) showEvent.VoidEvent += OnShow;
        if (exitEvent != null) exitEvent.VoidEvent += OnHide;
    }

    private void OnDisable()
    {
        if (showEvent != null) showEvent.VoidEvent -= OnShow;
        if (exitEvent != null) exitEvent.VoidEvent -= OnHide;
    }

    private void OnShow()
    {
        gameObject.SetActive(true);
        // 启动动画循环
        if (animRoutine == null) animRoutine = StartCoroutine(AnimateKey());
    }

    private void OnHide()
    {
        // 停止动画
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
        gameObject.SetActive(false);
    }

    /// <summary>在 frameUp / frameDown 之间循环切换，模拟按键动画。</summary>
    private IEnumerator AnimateKey()
    {
        bool down = false;
        // 确保初始显示 frameUp
        if (keyImage != null && frameUp != null) keyImage.sprite = frameUp;

        while (true)
        {
            yield return new WaitForSeconds(frameInterval);
            down = !down;
            if (keyImage != null)
            {
                keyImage.sprite = down ? frameDown : frameUp;
            }
        }
    }
}
