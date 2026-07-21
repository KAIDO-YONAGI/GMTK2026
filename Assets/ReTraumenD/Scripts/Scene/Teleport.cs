using UnityEngine;

public class SceneToggler : MonoBehaviour
{
    private const string PlayerTag = "Player";

    public SceneLoadEventSO loadEventSO;
    public Vector3 newPosition;
    public GameSceneSO sceneToLoad;
    public bool isToFade = true;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag(PlayerTag)) return;

        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
