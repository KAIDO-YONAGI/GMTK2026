using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InitialLoad : MonoBehaviour
{
    [Tooltip("Scenes loaded additively during startup and kept loaded.")]
    public List<string> persistentSceneNames = new List<string>();

    private void Awake()
    {
        StartCoroutine(LoadPersistentScenes());
    }

    private IEnumerator LoadPersistentScenes()
    {
        foreach (string sceneName in persistentSceneNames)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                continue;

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                continue;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"InitialLoad cannot load scene '{sceneName}'. Add it to Build Settings first.");
                continue;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;
        }
    }
}
