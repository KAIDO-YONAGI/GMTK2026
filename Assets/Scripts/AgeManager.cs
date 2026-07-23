using UnityEngine;

public class AgeManager : MonoBehaviour
{
    public static AgeManager instance;

    [SerializeField] private float startingAge = 20f;
    [SerializeField] private float maxAge = 100f;
    [SerializeField] private float agePerSecond = 1f;
    [SerializeField] private DeathEventSO deathEventSO;

    private float currentAge;

    public float CurrentAge => currentAge;
    public float AgeNormalized => Mathf.Clamp01((currentAge - startingAge) / (maxAge - startingAge));
    public bool IsDead => currentAge >= maxAge;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        currentAge = startingAge;
    }

    private void Update()
    {
        if (IsDead) return;

        if (TimeManager.instance != null && TimeManager.instance.IsGamePaused()) return;

        currentAge += agePerSecond * Time.deltaTime;

        if (currentAge >= maxAge)
        {
            currentAge = maxAge;
            deathEventSO?.RaiseDeathEvent(MyEnums.DeathType.OldDead);
        }
    }
}
