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
/// </summary>
public class EndingManager : MonoBehaviour
{
    [SerializeField] private ToggleCanvasEventSO endingToggleEventSO;
    [SerializeField] private CanvasGroup endingGroup;

    [Header("Scene Loaded Event")]
    [Tooltip("SceneChanger 场景加载完成事件，与 ending 事件双判后才显示面板。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("Fade Out")]
    [Tooltip("点击 The End 后画布淡出的时长（秒）。")]
    [SerializeField] private float fadeOutDuration = 1f;

    // 双判标志
    private bool _endingRequested;
    private bool _sceneLoaded;

    private Coroutine _fadeCoroutine;

    private void OnEnable()
    {
        if (endingToggleEventSO != null)
            endingToggleEventSO.toggleCanvasEvent += OnEndingToggle;
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (endingToggleEventSO != null)
            endingToggleEventSO.toggleCanvasEvent -= OnEndingToggle;
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
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
        endingGroup.alpha = 1;
        endingGroup.interactable = true;
        endingGroup.blocksRaycasts = true;
    }

    /// <summary>"The End" 按钮点击回调：淡出画布并取消遮挡。</summary>
    public void OnTheEndButton()
    {
        // 标记本次运行已通关，StartingMenu 据此显示并保留 "Again and Again..." 字样
        GoalTrigger.CompletedThisSession = true;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutRoutine());
        _endingRequested=false;
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
        _fadeCoroutine = null;
    }
}
