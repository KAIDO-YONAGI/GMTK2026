using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DifficultySettingsSO", menuName = "Difficulty/DifficultySettingsSO", order = 0)]
public class DifficultySettingsSO : ScriptableObject
{
    [Serializable]
    public struct SceneDifficulty
    {
        public GameSceneSO scene;
        public float agePerSecond;
    }

    public SceneDifficulty[] entries;
}
