using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class RetryManager : MonoBehaviour
{
    RetryManager instance;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    public List<GameSceneSO> currentScene = new List<GameSceneSO>();
    public SceneLoadEventSO loadEventSO;
    [Header("Retry Event")]
    public VoidEventSO retryEventSO;

    private void OnEnable()
    {
        retryEventSO.VoidEvent += OnReTry;
    }
    private void OnDisable()
    {
        retryEventSO.VoidEvent -= OnReTry;
    }
    private void OnReTry()
    {
        if (HealthManager.Instance != null)
            HealthManager.Instance.ResetHealth();

        loadEventSO.RaiseLoadRequestEvent(currentScene, Vector3.zero, true);
    }
}
