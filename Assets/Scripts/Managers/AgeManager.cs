using System.Collections.Generic;
using UnityEngine;

public class AgeManager : YSingleton<AgeManager>
{
    [SerializeField] private float startingAge = 20f;
    [SerializeField] private float maxAge = 100f;
    [SerializeField] private float agePerSecond = 1f;
    [SerializeField] private DeathEventSO_ deathEventSO;
    [SerializeField] private SceneLoadEventSO sceneLoadEventSO;
    [SerializeField] private DifficultySettingsSO difficultySettings;

    private bool isInLevel;
    private float currentAge;

    public float CurrentAge => currentAge;
    public float AgeNormalized => Mathf.Clamp01((currentAge - startingAge) / (maxAge - startingAge));
    public bool IsDead => currentAge >= maxAge;

    protected override void OnSingletonInitialized()
    {
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
        if (difficultySettings != null && difficultySettings.entries != null && scenes != null && scenes.Count > 0)
        {
            GameSceneSO targetScene = scenes[0];
            foreach (var entry in difficultySettings.entries)
            {
                if (entry.scene == targetScene)
                {
                    agePerSecond = entry.agePerSecond;
                    break;
                }
            }
        }

        isInLevel = true;
        currentAge = startingAge;
    }

    private void Update()
    {
        if (!isInLevel) return;
        if (IsDead) return;
        if (TimeManager.Instance != null && TimeManager.Instance.IsGamePaused()) return;

        // TODO: 后期合并代码后，需判断玩家单例是否存在再计时
        // if (Player.Instance == null) return;

        currentAge += agePerSecond * Time.deltaTime;

        if (currentAge >= maxAge)
        {
            currentAge = maxAge;
            deathEventSO?.RaiseDeathEvent(MyEnums.DeathType.OldDead);
        }
    }
}
