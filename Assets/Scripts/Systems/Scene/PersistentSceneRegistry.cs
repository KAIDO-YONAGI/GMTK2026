using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime registry for scenes that must never be unloaded by SceneChanger.
/// InitialLoad owns registration so business code does not duplicate this configuration.
/// </summary>
public static class PersistentSceneRegistry
{
    private static readonly HashSet<string> SceneNames = new HashSet<string>();

    public static void Register(IEnumerable<GameSceneSO> scenes)
    {
        if (scenes == null)
            return;

        foreach (GameSceneSO scene in scenes)
            Register(scene);
    }

    public static void Register(GameSceneSO scene)
    {
        if (scene == null || string.IsNullOrWhiteSpace(scene.sceneName))
            return;

        SceneNames.Add(scene.sceneName);
    }

    public static bool IsPersistent(GameSceneSO scene)
    {
        return scene != null && IsPersistent(scene.sceneName);
    }

    public static bool IsPersistent(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && SceneNames.Contains(sceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SceneNames.Clear();
    }
}
