using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneToggler : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public Vector3 newPosition;
    public GameSceneSO sceneToLoad;
    public bool isToFade = true;
    private void OnTriggerEnter2D(Collider2D collider)
    {
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
