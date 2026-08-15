using System.Collections;
using UnityEngine;

/// <summary>
/// 回溯导引画布：监听两个 VoidEventSO（显示与退出）控制显隐。
/// 使用 CanvasGroup 的透明度执行淡入淡出，R 键动画由 Animator（R_KeyAnimator）驱动。
/// 注意：通过透明度和射线阻挡控制可见性，不调用 SetActive，以免停掉 Animator。
/// 玩法事件负责业务侧显隐，ToggleCanvasEventSO 负责 UIManager 的互斥关闭与焦点刷新。
/// </summary>
public class GuideCanvas : MonoBehaviour, ICanvasManager
{
    [Header("事件")]
    [Tooltip("触发后淡入显示导引。")]
    [SerializeField] private VoidEventSO showEvent;

    [Tooltip("触发后淡出隐藏导引。")]
    [SerializeField] private VoidEventSO exitEvent;

    [Tooltip("供界面管理器执行互斥关闭和焦点刷新的导引面板事件。")]
    [SerializeField] private ToggleCanvasEventSO guideToggleEvent;

    [Tooltip("场景加载完成事件，加载完成后将导引面板设为 inactive。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("画布组")]
    [Tooltip("导引的画布组，控制透明度淡入淡出。")]
    [SerializeField] private CanvasGroup guideCanvasGroup;

    [Header("淡入淡出")]
    [Tooltip("淡入淡出时长（秒）。0 = 瞬间切换。")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("动画")]
    [Tooltip("按下 R 键时使用的动画控制器（播放 R_KeyAnimator）。")]
    [SerializeField] private Animator keyAnimator;

    [Header("界面管理器集成")]
    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("面板打开时，是否阻塞 ESC 键及其他全局面板输入。")]
    [SerializeField] private bool blocksGlobalInput = false;

    // 淡入淡出协程句柄
    private Coroutine fadeRoutine;
    private Canvas guideCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => guideToggleEvent;
    public VoidEventSO SceneLoadedEvent => sceneLoadedEvent;
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
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
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
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (guideCanvasGroup != null)
        {
            ((ICanvasManager)this).SetCanvaInactive(
                guideCanvasGroup, MyEnums.CanvasToToggle.Guide);
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
        // 淡出到完全透明，结束后禁用交互和射线阻挡。
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f));
    }

    /// <summary>
    /// 将 CanvasGroup 的透明度渐变到目标值。
    /// 目标透明度为 1 时启用射线阻挡和交互；为 0 时禁用。
    /// Animator 不受影响，GameObject 始终保持激活。
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

        // 时长为 0 时立即切换。
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            guideCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        guideCanvasGroup.alpha = targetAlpha;

        // 完全不透明时可见且可交互；完全透明时不可见且不可交互。
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
