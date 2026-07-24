using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 血量心 HUD：根据血量显示一排心，一滴血一颗心。
/// 订阅 HealthChangedEvent（IntEventSO，广播当前血量）：
///   - 满血时所有心可见（alpha=1）
///   - 扣血时对应位置的心淡出消失（留空位）
///   - 回血时新心淡入出现
/// 淡入淡出用 CanvasGroup.alpha + 协程（参照 GuideCanvas.FadeRoutine 模式）。
/// </summary>
public class HealthHeartUI : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("血量变化事件（HealthManager 广播当前血量）。")]
    [SerializeField] private IntEventSO healthChangedEvent;

    [Header("Heart Setup")]
    [Tooltip("单颗心预制件（需含 Image + CanvasGroup）。")]
    [SerializeField] private GameObject heartPrefab;

    [Tooltip("心组的父节点（一般挂 HorizontalLayoutGroup 自动横向排列）。")]
    [SerializeField] private Transform heartContainer;

    [Header("Fade")]
    [Tooltip("心淡入淡出时长（秒）。0 = 瞬间切换。")]
    [SerializeField] private float fadeDuration = 0.3f;

    // 所有心的 CanvasGroup（索引 0 是第一滴血/最左边的心）
    private readonly List<CanvasGroup> hearts = new List<CanvasGroup>();
    // 每颗心各自的淡出协程句柄，便于重复触发时先停旧的
    private readonly List<Coroutine> heartCoroutines = new List<Coroutine>();

    private void Start()
    {
        SpawnHearts();
        // 初始化显示为当前血量（避免开局闪烁）
        if (HealthManager.Instance != null)
            RefreshHeartVisibility(HealthManager.Instance.CurrentHealth, instant: true);
    }

    private void OnEnable()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.IntEvent += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.IntEvent -= OnHealthChanged;
    }

    /// <summary>按最大血量生成对应数量的心。</summary>
    private void SpawnHearts()
    {
        if (HealthManager.Instance == null || heartPrefab == null || heartContainer == null) return;

        int maxHealth = HealthManager.Instance.MaxHealth;
        for (int i = 0; i < maxHealth; i++)
        {
            GameObject heart = Instantiate(heartPrefab, heartContainer);
            var group = heart.GetComponent<CanvasGroup>();
            if (group == null) group = heart.AddComponent<CanvasGroup>();
            group.alpha = 1f; // 默认满血可见
            hearts.Add(group);
            heartCoroutines.Add(null);
        }
    }

    /// <summary>血量变化回调：刷新每颗心的可见性。</summary>
    private void OnHealthChanged(int currentHealth)
    {
        RefreshHeartVisibility(currentHealth, instant: false);
    }

    /// <summary>
    /// 按当前血量刷新所有心：索引 &lt; currentHealth 的心可见，其余淡出。
    /// instant=true 时瞬切（初始化用），否则走淡入淡出协程。
    /// </summary>
    private void RefreshHeartVisibility(int currentHealth, bool instant)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            float targetAlpha = i < currentHealth ? 1f : 0f;
            var group = hearts[i];
            if (group == null) continue;

            if (instant || fadeDuration <= 0f)
            {
                group.alpha = targetAlpha;
                continue;
            }

            // 停掉这颗心旧的淡出协程，再启动新的
            if (heartCoroutines[i] != null) StopCoroutine(heartCoroutines[i]);
            heartCoroutines[i] = StartCoroutine(FadeHeart(group, targetAlpha, i));
        }
    }

    /// <summary>单颗心的 alpha 渐变协程（参照 GuideCanvas.FadeRoutine）。</summary>
    private IEnumerator FadeHeart(CanvasGroup group, float targetAlpha, int index)
    {
        float startAlpha = group.alpha;
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            heartCoroutines[index] = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = targetAlpha;
        heartCoroutines[index] = null;
    }
}
