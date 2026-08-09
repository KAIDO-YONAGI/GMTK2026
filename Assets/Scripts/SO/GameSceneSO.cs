using UnityEngine;
using MyEnums;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GameSceneSO", menuName = "GameSceneSO/SceneSO", order = 0)]
public class GameSceneSO : ScriptableObject {
    public string ID;

#if UNITY_EDITOR
    [Tooltip("拖入场景文件（.unity），保存时自动同步到 sceneName")]
    public SceneAsset sceneAsset;
#endif

    [Tooltip("运行时实际加载的场景名。编辑器下由 sceneAsset 自动同步，一般无需手动修改")]
    public string sceneName;

    public SceneType sceneType;
    public Vector3 initialPosition;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(ID))
            ID = Guid.NewGuid().ToString();

#if UNITY_EDITOR
        if (sceneAsset != null)
        {
            string assetName = sceneAsset.name;
            if (assetName != sceneName)
                sceneName = assetName;

            EnsureSceneInBuildSettings();
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 确保由场景 SO 引用的场景可被运行时 SceneManager 加载。
    /// </summary>
    private void EnsureSceneInBuildSettings()
    {
        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        if (string.IsNullOrEmpty(scenePath))
            return;

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].path != scenePath)
                continue;

            if (!buildScenes[i].enabled)
            {
                buildScenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = buildScenes;
            }
            return;
        }

        Array.Resize(ref buildScenes, buildScenes.Length + 1);
        buildScenes[buildScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = buildScenes;
    }
#endif
}
