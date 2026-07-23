using System.Collections.Generic;
using UnityEngine;

public class AgeManager : MonoBehaviour
{
    public static AgeManager instance;

    [SerializeField] private float startingAge = 20f;
    [SerializeField] private float maxAge = 100f;
    [SerializeField] private float agePerSecond = 1f;
    [SerializeField] private DeathEventSO deathEventSO;
    [SerializeField] private SceneLoadEventSO sceneLoadEventSO;

    private bool isInLevel;
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

    private void OnEnable()
    {
        if (sceneLoadEventSO != null)
            sceneLoadEventSO.LoadRequestEvent += OnSceneLoadRequest;
    }

    private void OnDisable()
    {
        if (sceneLoadEventSO != null)
            sceneLoadEventSO.LoadRequestEvent -= OnSceneLoadRequest;
    }

    private void OnSceneLoadRequest(List<GameSceneSO> scenes, Vector3 position, bool isToFade)
    {
        bool hasLocation = scenes != null && scenes.Exists(s => s != null && s.sceneType == MyEnums.SceneType.Location);
        if (!hasLocation) return;

        isInLevel = true;
        currentAge = startingAge;
    }

    private void Update()
    {
        if (!isInLevel) return;
        if (IsDead) return;
        if (TimeManager.instance != null && TimeManager.instance.IsGamePaused()) return;

        // TODO: 后期合并代码后，需判断玩家单例是否存在再计时
        // if (Player.instance == null) return;

        currentAge += agePerSecond * Time.deltaTime;

        if (currentAge >= maxAge)
        {
            currentAge = maxAge;
            deathEventSO?.RaiseDeathEvent(MyEnums.DeathType.OldDead);
        }
    }
}
