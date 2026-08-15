using UnityEngine;
using TMPro;

public class GameOverManager : MonoBehaviour, ICanvasManager
{
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private TMP_Text deathText;

    [Header("Death Messages")]
    [Tooltip("老死提示词（DeathType.OldDead）")]
    [TextArea] [SerializeField] private string peacefulDeathMessage = "Rest in peace...";
    [Tooltip("非正常死亡提示词（被打死/坠落等，DeathType.UnnormalDead）")]
    [TextArea] [SerializeField] private string brutalDeathMessage = "Slaughtered without mercy!";

    private Canvas gameOverCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => gameOverToggleEventSO;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = true;
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
