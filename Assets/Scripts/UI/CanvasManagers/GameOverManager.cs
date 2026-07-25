using UnityEngine;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private TMP_Text deathText;

    [Header("Death Messages")]
    [Tooltip("老死提示词（DeathType.OldDead）")]
    [TextArea] [SerializeField] private string peacefulDeathMessage = "Rest in peace...";
    [Tooltip("非正常死亡提示词（被打死/坠落等，DeathType.UnnormalDead）")]
    [TextArea] [SerializeField] private string brutalDeathMessage = "Slaughtered without mercy!";

    private void OnEnable()
    {
        gameOverToggleEventSO.toggleCanvasEvent += OnGameOver;
    }

    private void OnDisable()
    {
        gameOverToggleEventSO.toggleCanvasEvent -= OnGameOver;
    }

    private void OnGameOver(bool state)
    {
        gameOverGroup.alpha = state ? 1 : 0;
        gameOverGroup.interactable = state;
        gameOverGroup.blocksRaycasts = state;

        // 打开面板时，根据死亡类型切换 deathText 的提示词
        if (state && deathText != null && DeathManager.instance != null)
        {
            deathText.text = DeathManager.instance.LastDeathType == MyEnums.DeathType.OldDead
                ? peacefulDeathMessage
                : brutalDeathMessage;
        }
    }
}
