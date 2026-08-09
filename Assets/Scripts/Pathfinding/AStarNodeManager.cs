using System.Collections.Generic;
using MyEnums;
using UnityEngine;
using UnityEngine.Tilemaps;
[DefaultExecutionOrder(-101)]

public class AStarNodeManager : MonoBehaviour
{
    public static AStarNodeManager instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
        InitMapInfo();
        InitiateNodes();
    }

    [Header("Tilemaps")]
    public Tilemap[] tilemaps;
    [Header("Grid Settings")]
    public LayerMask obstacleLayers;

    private float cellSize = 1f;
    private float safetyMargin = 0.3f;
    private Dictionary<(int x, int y), AStarNode> nodeCellMap;

    public Dictionary<(int x, int y), AStarNode> GetNodeMap() => nodeCellMap;
    public float GetCellSize() => cellSize;
    //从Unity本身的浮点网格转化成整形的离散网格 一方面方便操作 另一方面能减少算法负担
    public (int x, int y) WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int y = Mathf.FloorToInt(worldPos.y / cellSize);
        return (x, y);
    }

    public Vector3 CellToWorld(int cx, int cy)
    {
        Vector3 basePos = new Vector3(
            cx * cellSize + cellSize * 0.5f,
            cy * cellSize + cellSize * 0.5f,
            0
        );
        return ApplySafetyMargin(cx, cy, basePos);
    }
    //通过额外加上擦边通过的代价来让寻路路径远离边缘，从而避免卡脚
    private Vector3 ApplySafetyMargin(int cx, int cy, Vector3 worldPos)
    {
        if (safetyMargin <= 0) return worldPos;

        int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dy = { 1, 1, 0, -1, -1, -1, 0, 1 };
        float marginX = 0, marginY = 0;

        for (int i = 0; i < 8; i++)
        {
            var neighborKey = (cx + dx[i], cy + dy[i]);
            if (nodeCellMap.TryGetValue(neighborKey, out AStarNode neighbor))
            {
                if (neighbor.GetNodeType() == AStarNodeType.Obstacle)
                {
                    float realsafetyMargin = safetyMargin * cellSize;
                    if (dx[i] > 0) marginX = -realsafetyMargin;
                    else if (dx[i] < 0) marginX = realsafetyMargin;
                    else if (dy[i] > 0) marginY = -realsafetyMargin;
                    else if (dy[i] < 0) marginY = realsafetyMargin;
                }
            }
        }
        Vector3 optNode = new Vector3(worldPos.x + marginX, worldPos.y + marginY, 0);
        var optCell = WorldToCell(optNode);
        if (nodeCellMap[optCell].GetNodeType() != AStarNodeType.Obstacle)
            return optNode;
        else return worldPos;
    }

    private void InitMapInfo()
    {
        nodeCellMap = new Dictionary<(int x, int y), AStarNode>();
    }

    private void InitiateNodes()
    {
        if (tilemaps == null || tilemaps.Length == 0)
        {
            Debug.LogError("Tilemaps 未赋值！");
            return;
        }

        int subdivision = Mathf.RoundToInt(1f / cellSize);

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap currentTilemap = tilemaps[i];
            if (currentTilemap == null) continue;

            BoundsInt bounds = currentTilemap.cellBounds;

            foreach (Vector3Int cellPos in bounds.allPositionsWithin)
            {
                if (currentTilemap.HasTile(cellPos))
                {
                    string layerName = LayerMask.LayerToName(currentTilemap.gameObject.layer);
                    ProcessTileWithSubdivision(layerName, cellPos, subdivision);
                }
            }
        }

        ProcessColliderObstacles();
    }

    private void ProcessColliderObstacles()
    {
        Collider2D[] colliders = FindObjectsOfType<Collider2D>();
        int obstacleCount = 0;

        foreach (Collider2D col in colliders)
        {
            if ((obstacleLayers.value & (1 << col.gameObject.layer)) == 0) continue;

            Vector3 worldPos = col.transform.position;
            var (cx, cy) = WorldToCell(worldPos);
            var key = (cx, cy);

            if (nodeCellMap.TryGetValue(key, out AStarNode node))
            {
                node.SetNodeType(AStarNodeType.Obstacle);
            }
            else
            {
                nodeCellMap[key] = new AStarNode(cx, cy, AStarNodeType.Obstacle);
            }
            obstacleCount++;
        }
    }
    //按指定网格宽度细分原始网格
    private void ProcessTileWithSubdivision(string layerName, Vector3Int cellPos, int subdivision)
    {
        int startX = cellPos.x * subdivision;
        int startY = cellPos.y * subdivision;

        for (int i = 0; i < subdivision; i++)
        {
            for (int j = 0; j < subdivision; j++)
            {
                ProcessTile(layerName, startX + i, startY + j);
            }
        }
    }

    private void ProcessTile(string layerName, int cx, int cy)
    {
        var key = (cx, cy);

        switch (layerName)
        {
            case "Obstacle":
                if (nodeCellMap.ContainsKey(key))
                {
                    nodeCellMap[key].SetNodeType(AStarNodeType.Obstacle);
                }
                else
                {
                    nodeCellMap[key] = new AStarNode(cx, cy, AStarNodeType.Obstacle);
                }
                break;

            case "Walkable":
                if (nodeCellMap.ContainsKey(key))
                {
                    nodeCellMap[key].SetNodeType(AStarNodeType.Walkable);
                }
                else
                {
                    nodeCellMap[key] = new AStarNode(cx, cy, AStarNodeType.Walkable);
                }
                break;
        }
    }
}
