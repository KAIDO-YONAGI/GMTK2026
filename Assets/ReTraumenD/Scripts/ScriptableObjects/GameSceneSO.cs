using UnityEngine;
using UnityEngine.AddressableAssets;
using MyEnums;
[CreateAssetMenu(fileName = "GameSceneSO", menuName = "GameSceneSO/SceneSO", order = 0)]
public class GameSceneSO : ScriptableObject {
    public string ID;
    public  AssetReference sceneReference;
    public SceneType sceneType;
    public Vector3 initialPosition;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(ID))
            ID = System.Guid.NewGuid().ToString();
    }
}
