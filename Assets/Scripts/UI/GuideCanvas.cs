using System.Collections;
using UnityEngine;

/// <summary>
/// 回溯导引 Canvas：监听两个 VoidEventSO（show/exit）控制显隐。
/// 用 CanvasGroup 的 alpha 做淡入淡出，R 键按下动画由 Animator（R_KeyAnimator）驱动。
/// 注意：用 alpha+blocksRaycasts 控制可见，不SetActive，否则会停掉 Animator。
/// </summary>
public class GuideCanvas : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("触发后淡入显示导引。")]
    [SerializeField] private VoidEventSO showEvent;

    [Tooltip("触发后淡出隐藏导引。")]
    [SerializeField] private VoidEventSO exitEvent;

    [Header("Canvas Group")]
    [Tooltip("导引的 CanvasGroup，控制 alpha 淡入淡出。")]
    [SerializeField] private CanvasGroup guideCanvasGroup;

    [Header("Fade")]
    [Tooltip("淡入淡出时长（秒）。0 = 瞬间切换。")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Animation")]
    [Tooltip("R 键动画的 Animator（播放 R_KeyAnimator）。")]
    [SerializeField] private Animator keyAnimator;

    // 淡入淡出协程句柄
    private Coroutine fadeRoutine;

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
        // 淡入到 alpha=1，并启用交互/射线
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
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

        fadeRoutine = null;
    }
}
