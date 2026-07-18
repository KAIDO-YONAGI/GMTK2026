using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InitialLoad : MonoBehaviour
{
    [Tooltip("要 Additive 加载的常驻场景名（例如 PersistentScene）")]
    public string persistentSceneName;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(persistentSceneName))
            SceneManager.LoadSceneAsync(persistentSceneName, LoadSceneMode.Additive);
    }
}
