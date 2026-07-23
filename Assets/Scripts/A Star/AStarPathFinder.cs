using System;
using System.Collections.Generic;
using MyEnums;
using UnityEngine;

//TODO 可能的优化：小顶堆存开启列表，动态A*，距离算法的优化
//关于网格和世界坐标的转化 由于转化关系，需要先导航到这个网格中心点才能开始导航
//地图数据获取也可以优化，用以解决稀疏地图的遍历问题
//可以用带权路径替换开根计算
//细分单元格
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(AStarNodeManager))]//依赖保证（不存在会自动添加）
public class AStarPathFinder : MonoBehaviour
{
    public static AStarPathFinder instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    public Dictionary<(int x, int y), AStarNode> GetNodeMap() => AStarNodeManager.instance.GetNodeMap();
    public (int x, int y) WorldToCell(Vector3 worldPos) => AStarNodeManager.instance.WorldToCell(worldPos);
    public Vector3 CellToWorld(int cx, int cy) => AStarNodeManager.instance.CellToWorld(cx, cy);
    public float GetCellSize() => AStarNodeManager.instance.GetCellSize();

    private Dictionary<(int x, int y), AStarNode> NodeCellMap => AStarNodeManager.instance.GetNodeMap();

    public Stack<PathFinderDetails> FindPath(Vector3 optPos, Vector3 startPos, Vector3 endPos)
    {
        if (optPos == Vector3.zero) optPos = startPos;
        Dictionary<(int x, int y), PathFinderDetails> openDic = new();

        var startCell = WorldToCell(startPos);
        var endCell = WorldToCell(endPos);
        var optCell = WorldToCell(optPos);


        if (startCell != optCell
            && NodeCellMap.ContainsKey(optCell)
            && NodeCellMap[optCell].GetNodeType() == AStarNodeType.Walkable
            && NoCoverObstacleNodes(startCell, optCell))
        {
            startCell = optCell;
        }

        HashSet<(int x, int y)> closeSet = new();
        if ((!NodeCellMap.ContainsKey(startCell)) || (!NodeCellMap.ContainsKey(endCell)) || startCell == endCell)
        {
            return null;
        }
        PathFinderDetails startNode = new PathFinderDetails(startCell.x, startCell.y, endCell.x, endCell.y, null);
        openDic.Add(startCell, startNode);

        while (openDic.Count > 0)
        {
            var currentPos = SearchCheapestCost(openDic);
            PathFinderDetails current = openDic[currentPos];

            openDic.Remove(currentPos);
            closeSet.Add(currentPos);

            if (currentPos == endCell) return RetracePath(current);

            AddNodeToOpen(currentPos, endCell, openDic, closeSet, current);
        }

        return null;
    }
    private bool NoCoverObstacleNodes((int x, int y) startCell, (int x, int y) optCell)
    {
        float distance = Mathf.Sqrt(
            (optCell.x - startCell.x) * (optCell.x - startCell.x) +
            (optCell.y - startCell.y) * (optCell.y - startCell.y));

        float dirX = optCell.x - startCell.x;
        float dirY = optCell.y - startCell.y;
        float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
        if (len > 0) { dirX /= len; dirY /= len; }

        float step = 1 * GetCellSize();

        for (float threshold = step; threshold < distance; threshold += step)
        {
            int cx = (int)Math.Round(startCell.x + dirX * threshold);
            int cy = (int)Math.Round(startCell.y + dirY * threshold);

            var key = (cx, cy);
            if (NodeCellMap.TryGetValue(key, out AStarNode node))
            {
                if (node.GetNodeType() == AStarNodeType.Obstacle)
                {
                    return false;
                }
            }
        }

        return true;
    }
    private Stack<PathFinderDetails> RetracePath(PathFinderDetails endNode)
    {
        Stack<PathFinderDetails> path = new Stack<PathFinderDetails>();
        PathFinderDetails current = endNode;

        while (current != null)
        {
            path.Push(current);
            current = current.GetFatherNode();
        }

        return path;
    }

    private (int x, int y) SearchCheapestCost(Dictionary<(int x, int y), PathFinderDetails> openDic)
    {
        float minCost = float.MaxValue;
        (int x, int y) minCostPos = default;
        foreach (var node in openDic)
        {
            float currentCost = node.Value.GetCost();
            if (minCost > currentCost)
            {
                minCost = currentCost;
                minCostPos = node.Key;
            }
        }
        return minCostPos;
    }

    private void AddNodeToOpen(
        (int x, int y) currentPos,
        (int x, int y) endPos,
        Dictionary<(int x, int y), PathFinderDetails> openDic,
        HashSet<(int x, int y)> closeSet,
        PathFinderDetails current)
    {
        int cx = currentPos.x;
        int cy = currentPos.y;

        int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dy = { 1, 1, 0, -1, -1, -1, 0, 1 };

        for (int i = 0; i < 8; i++)
        {
            int nx = cx + dx[i];
            int ny = cy + dy[i];
            var neighborPos = (x: nx, y: ny);

            if (closeSet.Contains(neighborPos)) continue;
            if (!NodeCellMap.ContainsKey(neighborPos)) continue;
            if (NodeCellMap[neighborPos].GetNodeType() != AStarNodeType.Walkable) continue;
            if (NodeCellMap[neighborPos].GetNodeType() == AStarNodeType.Walkable && !CanWalkDiagonally(cx, cy, dx[i], dy[i])) continue;

            PathFinderDetails newNode = new PathFinderDetails(nx, ny, endPos.x, endPos.y, current);

            if (!openDic.ContainsKey(neighborPos))
            {
                openDic.Add(neighborPos, newNode);
            }
            else
            {
                if (newNode.GetDisToBeg() < openDic[neighborPos].GetDisToBeg())
                {
                    openDic[neighborPos] = newNode;
                }
            }
        }
    }

    private bool CanWalkDiagonally(int x, int y, int dx, int dy)
    {
        if (Math.Abs(dx * dy) == 1)
        {
            var xOffset = (x + dx, y);
            var yOffset = (x, y + dy);

            if (NodeCellMap[xOffset].GetNodeType() != AStarNodeType.Walkable &&
                NodeCellMap[yOffset].GetNodeType() != AStarNodeType.Walkable)
            {
                return false;
            }
        }
        return true;
    }
}
