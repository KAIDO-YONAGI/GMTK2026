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
/// Central retry coordinator. The persistent scene owns one instance and maps the
/// current content scene to the complete additive scene group that must be reloaded.
/// </summary>
public class RetryManager : YSingleton<RetryManager>
{
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Header("Retry Event")]
    [SerializeField] private VoidEventSO retryEventSO;

    [Header("Retry Config")]
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
