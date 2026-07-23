using UnityEngine;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private GameObject peacefulDeathText;

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

        if (peacefulDeathText != null)
            peacefulDeathText.SetActive(
                state && DeathManager.instance != null &&
                DeathManager.instance.LastDeathType == MyEnums.DeathType.OldDead);
    }
}
