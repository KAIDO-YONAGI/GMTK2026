using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InitialLoad : MonoBehaviour
{
    [Tooltip("Scenes loaded additively during startup and kept loaded.")]
    public List<GameSceneSO> persistentScenes = new List<GameSceneSO>();

    private void Awake()
    {
        StartCoroutine(LoadPersistentScenes());
    }

private IEnumerator LoadPersistentScenes()
    {
        foreach (GameSceneSO sceneReference in persistentScenes)
        {
            if (sceneReference == null)
                continue;

            string sceneName = sceneReference.sceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"InitialLoad cannot load '{sceneReference.name}' because it has no scene assigned.");
                continue;
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                continue;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"InitialLoad cannot load scene '{sceneName}'. Assign a scene in its GameSceneSO first.");
                continue;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;
        }
    }
}
