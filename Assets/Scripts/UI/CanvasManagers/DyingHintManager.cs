using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 濒死提示控制器。
/// 订阅 healthChangedEvent（IntEventSO）：血量到 0 显示提示，&gt; 0 隐藏。
/// 接收到死亡事件时立即隐藏提示，由 GameOver 面板接管。
/// 放弃按钮由 Awake 代码绑定，点击后触发 OnDie 进入真正死亡流程（GameOver 面板接管）。
///
/// 框架接入（仅上报层级）：显隐走 ICanvasManager.SetCanvaState 上报状态（参与焦点栈/互斥声明），
/// 但 dyingToggleEvent 不进 UIManager 的 toggleCanvasEvents 列表——
/// UIManager 从不主动关闭本面板，显隐仍完全由血量与死亡事件驱动（保持幂等）；
/// ESC 在濒死提示显示时照常唤起 ESC 菜单（UIManager 的"仅上报面板不挡 ESC"规则保证）。
/// </summary>
public class DyingHintManager : MonoBehaviour, ICanvasManager
{
    [Tooltip("濒死提示的画布组，控制可见性。")]
    [SerializeField] private CanvasGroup dyingHintGroup;

    [Tooltip("生命值变更事件。与生命值显示共用同一个 IntEventSO 资源。")]
    [SerializeField] private IntEventSO healthChangedEvent;

    [Tooltip("玩家死亡时用于隐藏濒死提示的死亡事件。")]
    [SerializeField] private DeathEventSO_ deathEvent;

    [Tooltip("濒死提示开关事件，用于订阅焦点刷新；不加入界面管理器的可驱动开关事件列表。")]
    [SerializeField] private ToggleCanvasEventSO dyingToggleEvent;

    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = false;

    private Canvas dyingHintCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => dyingToggleEvent;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        dyingHintCanvas = GetComponentInParent<Canvas>();

        if (dyingHintGroup != null)
        {
            dyingHintGroup.alpha = 0f;
            dyingHintGroup.interactable = false;
            dyingHintGroup.blocksRaycasts = false;
        }

        // 放弃按钮通过代码绑定，减少检查器配置依赖。
        var giveUpButton = GetComponentInChildren<Button>();
        if (giveUpButton != null)
            giveUpButton.onClick.AddListener(OnGiveUpButton);
    }

    private void OnEnable()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.IntEvent += OnHealthChanged;

        if (deathEvent != null)
            deathEvent.deathEvent += OnDeath;

        if (dyingToggleEvent != null)
            dyingToggleEvent.focusEvent += OnFocus;
    }

    private void OnDisable()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.IntEvent -= OnHealthChanged;

        if (deathEvent != null)
            deathEvent.deathEvent -= OnDeath;

        if (dyingToggleEvent != null)
            dyingToggleEvent.focusEvent -= OnFocus;
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(
            dyingHintCanvas, MyEnums.CanvasToToggle.Dying, true);
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

    private void OnDeath(MyEnums.DeathType _)
    {
        SetVisible(false);
    }

    /// <summary>放弃按钮回调：放弃自救，触发真正死亡。</summary>
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
        // 经 ICanvasManager.SetCanvaState 上报状态（参与焦点栈/互斥声明）；
        // 放弃按钮需在濒死时可点击，交互性与可见性保持一致。
        ((ICanvasManager)this).SetCanvaState(
            dyingHintGroup, MyEnums.CanvasToToggle.Dying, visible);
    }
}
