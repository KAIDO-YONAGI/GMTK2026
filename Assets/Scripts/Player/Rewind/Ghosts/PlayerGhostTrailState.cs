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

    public SpriteRenderer Renderer => Ghost != null ? Ghost.Renderer : null;
}
