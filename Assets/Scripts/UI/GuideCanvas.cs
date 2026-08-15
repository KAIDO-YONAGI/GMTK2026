using System.Collections;
using UnityEngine;

/// <summary>
/// 回溯导引 Canvas：监听两个 VoidEventSO（show/exit）控制显隐。
/// 用 CanvasGroup 的 alpha 做淡入淡出，R 键按下动画由 Animator（R_KeyAnimator）驱动。
/// 注意：用 alpha+blocksRaycasts 控制可见，不SetActive，否则会停掉 Animator。
/// gameplay 事件负责业务侧显隐，ToggleCanvasEventSO 负责 UIManager 的互斥关闭与焦点刷新。
/// </summary>
public class GuideCanvas : MonoBehaviour, ICanvasManager
{
    [Header("Events")]
    [Tooltip("触发后淡入显示导引。")]
    [SerializeField] private VoidEventSO showEvent;

    [Tooltip("触发后淡出隐藏导引。")]
    [SerializeField] private VoidEventSO exitEvent;

    [Tooltip("UIManager 用于互斥关闭和焦点刷新的 Guide 面板事件。")]
    [SerializeField] private ToggleCanvasEventSO guideToggleEvent;

    [Header("Canvas Group")]
    [Tooltip("导引的 CanvasGroup，控制 alpha 淡入淡出。")]
    [SerializeField] private CanvasGroup guideCanvasGroup;

    [Header("Fade")]
    [Tooltip("淡入淡出时长（秒）。0 = 瞬间切换。")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Animation")]
    [Tooltip("R 键动画的 Animator（播放 R_KeyAnimator）。")]
    [SerializeField] private Animator keyAnimator;

    [Header("UIManager Integration")]
    [Tooltip("Whether Escape may close this panel while it is focused.")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("Whether this panel blocks Escape and other global panel input while open.")]
    [SerializeField] private bool blocksGlobalInput = false;

    // 淡入淡出协程句柄
    private Coroutine fadeRoutine;
    private Canvas guideCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => guideToggleEvent;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        guideCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        if (showEvent != null) showEvent.VoidEvent += OnShow;
        if (exitEvent != null) exitEvent.VoidEvent += OnHide;
        if (guideToggleEvent != null)
        {
            guideToggleEvent.toggleCanvasEvent += OnToggleRequested;
            guideToggleEvent.focusEvent += OnFocus;
        }
    }

    private void OnDisable()
    {
        if (showEvent != null) showEvent.VoidEvent -= OnShow;
        if (exitEvent != null) exitEvent.VoidEvent -= OnHide;
        if (guideToggleEvent != null)
        {
            guideToggleEvent.toggleCanvasEvent -= OnToggleRequested;
            guideToggleEvent.focusEvent -= OnFocus;
        }
    }

    private void OnToggleRequested(bool state)
    {
        if (state)
            OnShow();
        else
            OnHide();
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(
            guideCanvas, MyEnums.CanvasToToggle.Guide, true);
    }

    private void OnShow()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReportCanvasState(
                MyEnums.CanvasToToggle.Guide, true, CloseOnEscape, BlocksGlobalInput);
            ((ICanvasManager)this).RefreshCanvaOrder(
                guideCanvas, MyEnums.CanvasToToggle.Guide, true);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(1f));
    }

    private void OnHide()
    {
        // 淡出到 alpha=0，结束后禁用交互/射线
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f));
    }

    /// <summary>
    /// 把 CanvasGroup 的 alpha 渐变到目标值。
    /// targetAlpha=1 时启用 blocksRaycasts/interactable；=0 时禁用。
    /// Animator 不受影响，GameObject 始终激活。
    /// </summary>
    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (guideCanvasGroup == null)
        {
            Debug.LogWarning("[GuideCanvas] guideCanvasGroup 未赋值，无法淡入淡出。");
            yield break;
        }

        float startAlpha = guideCanvasGroup.alpha;
        float elapsed = 0f;

        // duration=0 时瞬间切换
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            guideCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        guideCanvasGroup.alpha = targetAlpha;

        // alpha=1 时可见可交互；alpha=0 时不可见不可交互
        guideCanvasGroup.blocksRaycasts = targetAlpha > 0f;
        guideCanvasGroup.interactable = targetAlpha > 0f;

        if (targetAlpha <= 0f && UIManager.Instance != null)
        {
            UIManager.Instance.ReportCanvasState(
                MyEnums.CanvasToToggle.Guide, false, CloseOnEscape, BlocksGlobalInput);
        }

        fadeRoutine = null;
    }
}
