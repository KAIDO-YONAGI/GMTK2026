using UnityEngine;

/// <summary>残影采样时记录的渲染状态。</summary>
internal struct PlayerGhostVisualState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
    public Sprite Sprite;
    public Color Color;
    public bool FlipX;
    public bool FlipY;

    public bool Equals(PlayerGhostVisualState other)
    {
        return Position == other.Position
            && Rotation == other.Rotation
            && Scale == other.Scale
            && Sprite == other.Sprite
            && Color == other.Color
            && FlipX == other.FlipX
            && FlipY == other.FlipY;
    }
}

/// <summary>单个残影实例及其时间线状态。</summary>
internal sealed class PlayerGhostTrailEntry
{
    public float SnapshotTime;
    public float FadeElapsed;
    public Color SourceColor;
    public PlayerGhost Ghost;

    // 残影创建时记录的世界位形；残影挂在会移动的 ReincarnationManager 下，
    // 每帧用这些值把残影钉回原位，保证拖尾视觉与挂在场景根时一致。
    public Vector3 WorldPosition;
    public Quaternion WorldRotation;
    public Vector3 WorldScale;

    public SpriteRenderer Renderer => Ghost != null ? Ghost.Renderer : null;
}
