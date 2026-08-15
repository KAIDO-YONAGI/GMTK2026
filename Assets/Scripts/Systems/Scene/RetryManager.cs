using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class RetryConfig
{
    [SerializeField] private GameSceneSO matchScene;
    [FormerlySerializedAs("scenes")]
    [SerializeField] private List<GameSceneSO> scenesToReload = new List<GameSceneSO>();
    [SerializeField] private Vector3 spawnPosition;

    public GameSceneSO MatchScene => matchScene;
    public List<GameSceneSO> ScenesToReload => scenesToReload;
    public Vector3 SpawnPosition => spawnPosition;
}

/// <summary>
/// 集中式重试协调器。常驻场景只保留一个实例，按当前内容场景匹配
/// 需要完整重载的叠加场景组。
/// </summary>
public class RetryManager : YSingleton<RetryManager>
{
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Header("重试事件")]
    [SerializeField] private VoidEventSO retryEventSO;

    [Header("重试配置")]
    [SerializeField] private RetryConfig[] retryConfigs; // 按场景配置，由 PersistentScene 统一维护

    private void OnEnable()
    {
        if (retryEventSO != null)
            retryEventSO.VoidEvent += OnReTry;
    }

    private void OnDisable()
    {
        if (retryEventSO != null)
            retryEventSO.VoidEvent -= OnReTry;
    }

    private void OnReTry()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[RetryManager] SceneChanger is unavailable; retry was cancelled.");
            return;
        }

        GameSceneSO currentScene = SceneChanger.Instance.GetCurrentGameScene();
        RetryConfig config = GetConfig(currentScene);
        if (config == null)
        {
            Debug.LogWarning($"[RetryManager] No retry config matches scene '{currentScene?.name ?? "<none>"}'.");
            return;
        }

        List<GameSceneSO> scenes = config.ScenesToReload;
        if (scenes == null || scenes.Count == 0)
        {
            Debug.LogError($"[RetryManager] Retry config for '{config.MatchScene.name}' has no scenes to reload.");
            return;
        }

        foreach (GameSceneSO scene in scenes)
        {
            if (scene == null)
            {
                Debug.LogError($"[RetryManager] Retry config for '{config.MatchScene.name}' contains a null scene.");
                return;
            }

            if (PersistentSceneRegistry.IsPersistent(scene))
            {
                Debug.LogError(
                    $"[RetryManager] Retry config for '{config.MatchScene.name}' references persistent scene " +
                    $"'{scene.sceneName}'. Retry was cancelled to protect persistent state.");
                return;
            }
        }

        if (loadEventSO == null)
        {
            Debug.LogError("[RetryManager] Load Event SO is not assigned; retry was cancelled.");
            return;
        }

        Vector3 position = config.SpawnPosition == Vector3.zero
            ? config.MatchScene.initialPosition
            : config.SpawnPosition;
        loadEventSO.RaiseLoadRequestEvent(new List<GameSceneSO>(scenes), position, true);
    }

    private RetryConfig GetConfig(GameSceneSO scene)
    {
        if (retryConfigs == null || scene == null) return null;
        foreach (var config in retryConfigs)
        {
            if (config != null && config.MatchScene == scene)
                return config;
        }
        return null;
    }
}
