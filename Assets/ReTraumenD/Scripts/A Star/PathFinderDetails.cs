public class PathFinderDetails
{
    private int x;
    private int y;
    private float cost;
    private float disToBeg;
    private float disToEnd;
    private PathFinderDetails fatherNode;

    public float GetCost() => cost;
    public float GetDisToBeg() => disToBeg;
    public int GetX() => x;
    public int GetY() => y;

    public PathFinderDetails GetFatherNode() => fatherNode;

    public PathFinderDetails(int x, int y, int endX, int endY, PathFinderDetails fatherNode)
    {
        this.x = x;
        this.y = y;
        this.fatherNode = fatherNode;

        if (fatherNode == null)
        {
            disToBeg = 0;
        }
        else
        {
            disToBeg = fatherNode.disToBeg +
                CalDistance(x, y, fatherNode.x, fatherNode.y);
        }

        disToEnd = CalDistance(x, y, endX, endY);
        cost = disToBeg + disToEnd;
    }

    private float CalDistance(int ax, int ay, int bx, int by)
    {
        int distance = Abs(bx - ax) + Abs(by - ay);
        return distance >= 2 ? 1.414f : 1;
    }

    private static int Abs(int v) => v < 0 ? -v : v;
}
