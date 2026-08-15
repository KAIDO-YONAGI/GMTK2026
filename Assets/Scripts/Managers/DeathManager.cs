using UnityEngine;

public class DeathManager : YSingleton<DeathManager>
{
    [SerializeField] private DeathEventSO_ deathEventSO;
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;

    public MyEnums.DeathType LastDeathType { get; private set; }

    private void OnEnable()
    {
        deathEventSO.deathEvent += OnDeath;
    }

    private void OnDisable()
    {
        deathEventSO.deathEvent -= OnDeath;
    }

    private void OnDeath(MyEnums.DeathType type)
    {
        LastDeathType = type;
        TimeManager.Instance.PauseGame();
        gameOverToggleEventSO.RaiseToggleCanvasEvent(true);
    }
}
