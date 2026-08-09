using UnityEngine;

/// <summary>
/// 描述某个场景特有的美术资产（当前仅音频）。
/// 每个内容场景对应一个 SO 实例，由场景内的 SceneArtAssetsTrigger 引用。
/// 通过 ownerScene 字段与 GameSceneSO 关联，用于事件匹配。
/// </summary>
[CreateAssetMenu(fileName = "SceneArtAssetsSO", menuName = "ArtAssets/SceneArtAssetsSO", order = 0)]
public class SceneArtAssetsSO : ScriptableObject
{
    [Header("Scene Identity")]
    [Tooltip("The scene this SO belongs to, used for event matching")]
    public GameSceneSO ownerScene;

    [Header("Background Music")]
    [Tooltip("Looped BGM for this scene")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("Ambient (Loop)")]
    [Tooltip("Looped ambient sound (rain/wind/noise etc.)")]
    public AudioClip ambient;
    [Range(0f, 1f)] public float ambientVolume = 0.3f;
}
