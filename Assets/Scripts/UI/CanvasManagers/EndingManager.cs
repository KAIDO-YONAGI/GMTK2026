using UnityEngine;
using System.Collections;

/// <summary>
/// 通关结束面板（位于 StartingMenu 场景内）。
///
/// 打开条件：同时收到 ending 事件 和 loaded 事件（双判）才显示面板。
/// - ending 事件（endingToggleEventSO）：GoalTrigger 通关时发出，标记"应该显示结局"。
/// - loaded 事件（sceneLoadedEvent）：SceneChanger 场景加载完成时发出，确保 StartingMenu 已就绪。
/// 两者都满足才打开面板，避免首次进游戏或单事件误触发。
///
/// 点击 The End → 协程淡出 CanvasGroup 并取消遮挡。
///
/// 框架接入（仅上报层级）：实现 ICanvasManager 并在显示/淡出完成时上报状态，
/// 但 endingToggleEventSO 不进 UIManager 的 toggleCanvasEvents 列表——
/// UIManager 从不主动关本面板，双判标志也不会被 ResetCanvas 广播清除（保幂等）。
/// </summary>
public class EndingManager : MonoBehaviour, ICanvasManager
{
    [SerializeField] private ToggleCanvasEventSO endingToggleEventSO;
    [SerializeField] private CanvasGroup endingGroup;

    [Header("Scene Loaded Event")]
    [Tooltip("SceneChanger 场景加载完成事件，与 ending 事件双判后才显示面板。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("Fade Out")]
    [Tooltip("点击 The End 后画布淡出的时长（秒）。")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Tooltip("Whether Escape may close this panel while it is focused.")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = true;

    // 双判标志
    private bool _endingRequested;
    private bool _sceneLoaded;

    private Coroutine _fadeCoroutine;
    private Canvas endingCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => endingToggleEventSO;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        endingCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        if (endingToggleEventSO != null)
        {
            endingToggleEventSO.toggleCanvasEvent += OnEndingToggle;
            endingToggleEventSO.focusEvent += OnFocus;
        }
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (endingToggleEventSO != null)
        {
            endingToggleEventSO.toggleCanvasEvent -= OnEndingToggle;
            endingToggleEventSO.focusEvent -= OnFocus;
        }
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnFocus()
    {

    }

    private void OnEndingToggle(bool state)
    {
        _endingRequested = state;
        // 收到 ending 请求时重置 loaded 标志，强制等待"这一次"的场景加载完成事件
        // 避免游戏启动时的旧 loaded 标志污染双判
        _sceneLoaded = false;
        TryShow();
    }

    private void OnSceneLoaded()
    {
        _sceneLoaded = true;
        TryShow();
    }

    /// <summary>双判：ending 和 loaded 都满足才显示面板，显示后立即重置标志（避免与下次加载误配对）。</summary>
    private void TryShow()
    {
        if (_endingRequested && _sceneLoaded)
        {
            _endingRequested = false;
            _sceneLoaded = false;
            ShowEnding();
        }
    }

    private void ShowEnding()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        ((ICanvasManager)this).ToggleCanvas(
            endingGroup, endingCanvas, MyEnums.CanvasToToggle.Ending, true);
    }

    /// <summary>"The End" 按钮点击回调：淡出画布并取消遮挡。</summary>
    public void OnTheEndButton()
    {
        // 标记本次运行已通关，StartingMenu 
        GoalTrigger.CompletedThisSession = true;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutRoutine());
        _endingRequested = false;
    }

    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = endingGroup.alpha;
        float elapsed = 0f;
        endingGroup.interactable = false;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            endingGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        endingGroup.alpha = 0f;
        endingGroup.blocksRaycasts = false;

        //淡出完成后回报关闭，解除输入阻塞（SetCanvaState 会重设 alpha/交互，此处值已一致）
        ((ICanvasManager)this).SetCanvaState(
            endingGroup, MyEnums.CanvasToToggle.Ending, false);
        _fadeCoroutine = null;
    }
}
