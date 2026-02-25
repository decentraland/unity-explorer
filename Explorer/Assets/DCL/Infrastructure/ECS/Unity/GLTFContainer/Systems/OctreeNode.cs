using UnityEngine;

public class OctreeNode
{
    public enum CellStatus
    {
        Unknown,
        Inside,
        Outside,
        Intersecting  // Contains geometry
    }

    public Bounds bounds;
    public CellStatus status = CellStatus.Unknown;
    public OctreeNode[] children = null; // null = leaf node

    // For propagation
    public int confidenceInside = 0;
    public int confidenceOutside = 0;
    public bool isKnown = false;
    public bool isSeed = false;

    // For octree construction
    public bool containsGeometry = false;

    public OctreeNode(Bounds bounds)
    {
        this.bounds = bounds;
    }

    public bool IsLeaf => children == null;

    public void Subdivide()
    {
        if (!IsLeaf) return;

        children = new OctreeNode[8];
        Vector3 center = bounds.center;
        Vector3 childSize = bounds.size / 2f;
        Vector3 quarterSize = bounds.size / 4f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 offset = new Vector3(
                (i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1,
                (i & 4) == 0 ? -1 : 1
            );

            Vector3 childCenter = center + Vector3.Scale(offset, quarterSize);
            Bounds childBounds = new Bounds(childCenter, childSize);

            children[i] = new OctreeNode(childBounds);
        }
    }

    public void GetAllLeaves(System.Collections.Generic.List<OctreeNode> leaves)
    {
        if (IsLeaf)
        {
            leaves.Add(this);
        }
        else
        {
            foreach (var child in children)
            {
                child.GetAllLeaves(leaves);
            }
        }
    }

    public OctreeNode GetLeafContaining(Vector3 point)
    {
        if (!bounds.Contains(point))
            return null;

        if (IsLeaf)
            return this;

        foreach (var child in children)
        {
            var result = child.GetLeafContaining(point);
            if (result != null)
                return result;
        }

        return null;
    }
}
