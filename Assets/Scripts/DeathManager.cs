using UnityEngine;

public class DeathManager : MonoBehaviour
{
    public static DeathManager instance;

    [SerializeField] private DeathEventSO_ deathEventSO;
    [SerializeField] private ToggleCanvasEventSO gameOverToggleEventSO;

    public MyEnums.DeathType LastDeathType { get; private set; }

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

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
        TimeManager.instance.PauseGame();
        gameOverToggleEventSO.RaiseToggleCanvasEvent(true);
    }
}
