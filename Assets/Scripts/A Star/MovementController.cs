using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MovementController : MonoBehaviour
{
    private Stack<PathFinderDetails> path = null;
    private float threshold = 0.5f;
    private float pathRebuildDistance = .5f;
    private float pathRebuildCooldown = .5f;
    private float pathRebuildTimer;
    private bool showPath = true;
    private Vector3 startPos;
    private Vector3 endPos;
    private bool hasValidPath = false;

    private float GetCellSize()
    {
        return AStarNodeManager.instance.GetCellSize();
    }

    public Vector3 GetPosToGo(Vector3 optPos, Vector3 startPos, Vector3 endPos)
    {
        if (pathRebuildTimer > 0)
            pathRebuildTimer -= Time.deltaTime;

        if (!hasValidPath || path == null || path.Count == 0)
        {
            if (!FindWay(optPos, startPos, endPos)) return Vector3.zero;
        }
        else
        {
            float distToTarget = (endPos - this.endPos).sqrMagnitude;
            float realPathRebuildDistance = pathRebuildDistance * GetCellSize();
            if (distToTarget > realPathRebuildDistance * realPathRebuildDistance && pathRebuildTimer <= 0)
            {
                ReFindWay(optPos, startPos, endPos);
                pathRebuildTimer = pathRebuildCooldown;
            }
        }

        if (path == null || path.Count == 0) return Vector3.zero;
        PathFinderDetails peek = path.Peek();
        return CellToWorld(peek.GetX(), peek.GetY());
    }

    public void ArrivedPos()
    {
        if (path != null && path.Count > 0)
        {
            path.Pop();
        }
    }
    public void ResetPath()
    {
        path = null;
        startPos = Vector3.zero;
        endPos = Vector3.zero;
        hasValidPath = false;
    }
    public float GetThreshold()
    {
        if (AStarNodeManager.instance == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("AStarNodeManager.instance is NULL!");
#endif

            return 0f;
        }

        return threshold * AStarNodeManager.instance.GetCellSize();
    }
    private bool FindWay(Vector3 optPos, Vector3 startPos, Vector3 endPos)
    {
        this.startPos = startPos;
        this.endPos = endPos;

        if (AStarPathFinder.instance != null)
            path = AStarPathFinder.instance.FindPath(optPos, startPos, endPos);

        hasValidPath = path != null && path.Count > 0;
        if (!hasValidPath)
        {
#if UNITY_EDITOR
            Debug.LogWarning("找不到路径！");
#endif
            return false;
        }
        return true;
    }

    private void ReFindWay(Vector3 optPos, Vector3 startPos, Vector3 endPos)
    {
        if (AStarPathFinder.instance == null)
        {
            path = null;
            hasValidPath = false;
            return;
        }

        PathFinderDetails[] currentPath = path.ToArray();

        Stack<PathFinderDetails> newPath = AStarPathFinder.instance.FindPath(optPos, startPos, endPos);

        if (newPath == null || newPath.Count == 0)
        {
            Debug.Log("ReFindWay()找不到路径！");
            path = null;
            hasValidPath = false;
            return;
        }

        PathFinderDetails[] newPathArray = newPath.ToArray();

        if (currentPath.Length >= 2 && newPathArray.Length >= 2)
        {
            Vector3 currentSecondNode = CellToWorld(currentPath[1].GetX(), currentPath[1].GetY());
            Vector3 newFirstNode = CellToWorld(newPathArray[1].GetX(), newPathArray[1].GetY());

            float distCurrent = (currentSecondNode - endPos).sqrMagnitude;
            float distNew = (newFirstNode - endPos).sqrMagnitude;

            if (distCurrent <= distNew)
            {
                this.endPos = endPos;
                hasValidPath = true;
                return;
            }
        }

        path = newPath;
        this.startPos = startPos;
        this.endPos = endPos;
        hasValidPath = true;
    }

    private void OnDrawGizmos()
    {
        Color pathColor = Color.yellow;
        Color startColor = Color.green;
        Color endColor = Color.red;
        float nodeRadius = 0.2f;
        if (!showPath || path == null || path.Count == 0) return;

        PathFinderDetails[] pathArray = path.ToArray();

        Gizmos.color = startColor;
        var startCell = WorldToCell(startPos);
        Gizmos.DrawWireSphere(CellToWorld(startCell.x, startCell.y), nodeRadius);

        Gizmos.color = endColor;
        var endCell = WorldToCell(endPos);
        Gizmos.DrawWireSphere(CellToWorld(endCell.x, endCell.y), nodeRadius);

        Gizmos.color = pathColor;
        for (int i = pathArray.Length - 1; i >= 0; i--)
        {
            Vector3 worldPos = CellToWorld(pathArray[i].GetX(), pathArray[i].GetY());
            Gizmos.DrawWireSphere(worldPos, nodeRadius);
        }

        Gizmos.color = pathColor;
        for (int i = 0; i < pathArray.Length - 1; i++)
        {
            Vector3 from = CellToWorld(pathArray[i].GetX(), pathArray[i].GetY());
            Vector3 to = CellToWorld(pathArray[i + 1].GetX(), pathArray[i + 1].GetY());
            Gizmos.DrawLine(from, to);
        }
    }

    private (int x, int y) WorldToCell(Vector3 worldPos)
    {
        return AStarPathFinder.instance != null ?
            AStarPathFinder.instance.WorldToCell(worldPos) : (0, 0);
    }
    private Vector3 CellToWorld(int cx, int cy)
    {
        return AStarPathFinder.instance != null ?
            AStarPathFinder.instance.CellToWorld(cx, cy) : Vector3.zero;
    }

}