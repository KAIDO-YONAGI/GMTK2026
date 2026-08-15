using UnityEngine;

/// <summary>
/// 描述某个场景特有的美术资产（当前仅音频）。
/// 每个内容场景对应一个 SO 实例，由场景内的 SceneArtAssetsTrigger 引用。
/// 通过 ownerScene 字段与 GameSceneSO 关联，用于事件匹配。
/// </summary>
[CreateAssetMenu(fileName = "SceneArtAssetsSO", menuName = "ArtAssets/SceneArtAssetsSO", order = 0)]
public class SceneArtAssetsSO : ScriptableObject
{
    [Header("场景标识")]
    [Tooltip("本 SO 所属的场景，用于匹配场景事件。")]
    public GameSceneSO ownerScene;

    [Header("背景音乐")]
    [Tooltip("本场景循环播放的背景音乐。")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("环境音（循环）")]
    [Tooltip("循环播放的环境声音效，例如雨声、风声或环境噪声。")]
    public AudioClip ambient;
    [Range(0f, 1f)] public float ambientVolume = 0.3f;
}
