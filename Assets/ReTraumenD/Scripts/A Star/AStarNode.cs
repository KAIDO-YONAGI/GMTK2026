using UnityEngine;
using MyEnums;
public class AStarNode
{
    private int x;
    private int y;
    private AStarNodeType nodeType;

    public AStarNode(int x, int y, AStarNodeType nodeType)
    {
        this.x = x;
        this.y = y;
        this.nodeType = nodeType;
    }

    public int GetX() => x;
    public int GetY() => y;
    public AStarNodeType GetNodeType() => nodeType;

    public void SetNodeType(AStarNodeType newType)
    {
        if (CanOverride(nodeType, newType))
        {
            nodeType = newType;
        }
    }

    private bool CanOverride(AStarNodeType existing, AStarNodeType newType)
    {
        if (newType == AStarNodeType.Obstacle) return true;
        if (existing == AStarNodeType.Obstacle) return false;
        return true;
    }
}
