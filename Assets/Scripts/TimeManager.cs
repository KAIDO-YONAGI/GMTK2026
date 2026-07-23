using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager instance;
    private bool isGamePaused;
    private int pauseCount = 0;
    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }
    public bool IsGamePaused()
    {
        return isGamePaused;
    }
    public void PauseGame()
    {
        pauseCount++;
        if (pauseCount > 1) return;

        Time.timeScale = 0;
        isGamePaused = true;
    }

    public void ResumeGame()
    {
        if (pauseCount == 0) return;

        pauseCount--;
        if (pauseCount > 0) return;

        Time.timeScale = 1;
        isGamePaused = false;
    }
    // 强制清除所有暂停请求，适用于重置游戏或切换场景。
    public void ForceResumeGame()
    {
        pauseCount = 0;
        Time.timeScale = 1;
        isGamePaused = false;
    }
}
