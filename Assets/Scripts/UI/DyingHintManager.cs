using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 濒死提示控制器。
/// 订阅 healthChangedEvent（IntEventSO）：血量到 0 显示提示，&gt; 0 隐藏。
/// Give Up 按钮由 Awake 代码绑定，点击后触发 OnDie 进入真正死亡流程（GameOver 面板接管）。
/// </summary>
public class DyingHintManager : MonoBehaviour
{
    [Tooltip("濒死提示的 CanvasGroup，控制可见性。")]
    [SerializeField] private CanvasGroup dyingHintGroup;

    [Tooltip("血量变化事件。与血量 HUD 订阅同一个 IntEventSO 资产。")]
    [SerializeField] private IntEventSO healthChangedEvent;

    private void Awake()
    {
        if (dyingHintGroup != null)
        {
            dyingHintGroup.alpha = 0f;
            dyingHintGroup.interactable = false;
            dyingHintGroup.blocksRaycasts = false;
        }

        // Give Up 按钮通过代码绑定，减少 Inspector 配置依赖。
        var giveUpButton = GetComponentInChildren<Button>();
        if (giveUpButton != null)
            giveUpButton.onClick.AddListener(OnGiveUpButton);
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

    private void OnHealthChanged(int health)
    {
        if (dyingHintGroup == null) return;

        // 真正死亡后隐藏濒死提示，由 GameOver 面板接管。
        if (HealthManager.Instance != null && HealthManager.Instance.IsDead)
        {
            SetVisible(false);
            return;
        }

        // 血量到 0 显示濒死提示；回血后隐藏。
        SetVisible(health <= 0);
    }

    /// <summary>Give Up 按钮回调：放弃自救，触发真正死亡。</summary>
    public void OnGiveUpButton()
    {
        // 立即隐藏濒死提示，避免与随后弹出的 GameOver 面板重叠。
        SetVisible(false);

        if (HealthManager.Instance != null)
            HealthManager.Instance.OnDie();
    }

    private void SetVisible(bool visible)
    {
        if (dyingHintGroup == null) return;
        // Give Up 按钮需要在濒死时可点击，因此 blocksRaycasts 跟随可见性。
        dyingHintGroup.alpha = visible ? 1f : 0f;
        dyingHintGroup.interactable = visible;
        dyingHintGroup.blocksRaycasts = visible;
    }
}
