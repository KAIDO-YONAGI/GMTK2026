using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存绝不能由 SceneChanger 卸载的常驻场景运行时注册表。
/// 由 InitialLoad 统一注册，业务代码无需重复配置。
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
