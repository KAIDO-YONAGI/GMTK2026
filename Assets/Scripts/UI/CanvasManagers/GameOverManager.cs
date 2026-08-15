using UnityEngine;
using TMPro;

public class GameOverManager : MonoBehaviour, ICanvasManager
{
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private TMP_Text deathText;

    [Header("死亡提示文本")]
    [Tooltip("老死时显示的提示文本。")]
    [TextArea] [SerializeField] private string peacefulDeathMessage = "Rest in peace...";
    [Tooltip("非正常死亡时显示的提示文本，例如被击杀或坠落。")]
    [TextArea] [SerializeField] private string brutalDeathMessage = "Slaughtered without mercy!";

    private Canvas gameOverCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => gameOverToggleEventSO;

    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = true;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        gameOverCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        gameOverToggleEventSO.toggleCanvasEvent += OnGameOver;
        gameOverToggleEventSO.focusEvent += OnFocus;
    }

    private void OnDisable()
    {
        gameOverToggleEventSO.toggleCanvasEvent -= OnGameOver;
        gameOverToggleEventSO.focusEvent -= OnFocus;
    }

    private void OnGameOver(bool state)
    {
        // 打开面板时，根据死亡类型切换 deathText 的提示词
        if (state && deathText != null && DeathManager.Instance != null)
        {
            deathText.text = DeathManager.Instance.LastDeathType == MyEnums.DeathType.OldDead
                ? peacefulDeathMessage
                : brutalDeathMessage;
        }

        ((ICanvasManager)this).ToggleCanvas(
            gameOverGroup, gameOverCanvas, MyEnums.CanvasToToggle.GameOver, state);
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(
            gameOverCanvas, MyEnums.CanvasToToggle.GameOver, true);
    }
}
