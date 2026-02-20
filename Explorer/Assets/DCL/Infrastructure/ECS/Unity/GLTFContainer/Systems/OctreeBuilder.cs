using UnityEngine;
using System.Collections.Generic;

public class OctreeBuilder
{
    public OctreeNode BuildOctree(Mesh mesh, Transform transform, int maxDepth, Bounds? forcedBounds = null)
    {
        // Calculate bounds
        Bounds bounds = forcedBounds ?? CalculateBounds(mesh, transform);
        bounds.Expand(0.1f); // Small padding

        Debug.Log($"Building octree with bounds: {bounds}, max depth: {maxDepth}");

        // Create root
        OctreeNode root = new OctreeNode(bounds);

        // Get triangles
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;

        // Subdivide recursively
        SubdivideNode(root, vertices, indices, transform, 0, maxDepth);

        // Count nodes
        List<OctreeNode> allLeaves = new List<OctreeNode>();
        root.GetAllLeaves(allLeaves);
        Debug.Log($"Octree built: {allLeaves.Count} leaf nodes");

        int geometryNodes = 0;
        foreach (var leaf in allLeaves)
            if (leaf.containsGeometry) geometryNodes++;

        Debug.Log($"Nodes containing geometry: {geometryNodes}");

        return root;
    }

    private void SubdivideNode(OctreeNode node, Vector3[] vertices, int[] indices,
                               Transform transform, int depth, int maxDepth)
    {
        // Check if any triangles intersect this node
        bool intersectsGeometry = TestGeometryIntersection(node.bounds, vertices, indices, transform);

        if (!intersectsGeometry)
        {
            // Empty space - mark as outside (will be confirmed later)
            node.status = OctreeNode.CellStatus.Unknown;
            return;
        }

        node.containsGeometry = true;

        // If at max depth, stop here
        if (depth >= maxDepth)
        {
            node.status = OctreeNode.CellStatus.Intersecting;
            return;
        }

        // Subdivide and recurse
        node.Subdivide();
        foreach (var child in node.children)
        {
            SubdivideNode(child, vertices, indices, transform, depth + 1, maxDepth);
        }
    }

    private bool TestGeometryIntersection(Bounds bounds, Vector3[] vertices, int[] indices, Transform transform)
    {
        Vector3 center = bounds.center;
        Vector3 halfExtents = bounds.extents;

        // Test each triangle
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = transform != null ? transform.TransformPoint(vertices[indices[i]]) : vertices[indices[i]];
            Vector3 v1 = transform != null ? transform.TransformPoint(vertices[indices[i + 1]]) : vertices[indices[i + 1]];
            Vector3 v2 = transform != null ? transform.TransformPoint(vertices[indices[i + 2]]) : vertices[indices[i + 2]];

            if (TriangleAABBIntersect(center, halfExtents, v0, v1, v2))
                return true;
        }

        return false;
    }

    private Bounds CalculateBounds(Mesh mesh, Transform transform)
    {
        if (transform == null)
            return mesh.bounds;

        Vector3[] corners = new Vector3[8];
        Bounds localBounds = mesh.bounds;
        Vector3 c = localBounds.center;
        Vector3 e = localBounds.extents;

        corners[0] = transform.TransformPoint(c + new Vector3(e.x, e.y, e.z));
        corners[1] = transform.TransformPoint(c + new Vector3(e.x, e.y, -e.z));
        corners[2] = transform.TransformPoint(c + new Vector3(e.x, -e.y, e.z));
        corners[3] = transform.TransformPoint(c + new Vector3(e.x, -e.y, -e.z));
        corners[4] = transform.TransformPoint(c + new Vector3(-e.x, e.y, e.z));
        corners[5] = transform.TransformPoint(c + new Vector3(-e.x, e.y, -e.z));
        corners[6] = transform.TransformPoint(c + new Vector3(-e.x, -e.y, e.z));
        corners[7] = transform.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z));

        Bounds worldBounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 8; i++)
            worldBounds.Encapsulate(corners[i]);

        return worldBounds;
    }

    // Simplified triangle-AABB test (CPU version)
    private bool TriangleAABBIntersect(Vector3 boxCenter, Vector3 boxHalfSize, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        // Move box to origin
        v0 -= boxCenter;
        v1 -= boxCenter;
        v2 -= boxCenter;

        // Test AABB (simple version)
        Vector3 min = Vector3.Min(Vector3.Min(v0, v1), v2);
        Vector3 max = Vector3.Max(Vector3.Max(v0, v1), v2);

        if (min.x > boxHalfSize.x || max.x < -boxHalfSize.x) return false;
        if (min.y > boxHalfSize.y || max.y < -boxHalfSize.y) return false;
        if (min.z > boxHalfSize.z || max.z < -boxHalfSize.z) return false;

        // Test plane (simplified)
        Vector3 e0 = v1 - v0;
        Vector3 e1 = v2 - v1;
        Vector3 normal = Vector3.Cross(e0, e1);

        float d = -Vector3.Dot(normal, v0);
        float r = boxHalfSize.x * Mathf.Abs(normal.x) +
                  boxHalfSize.y * Mathf.Abs(normal.y) +
                  boxHalfSize.z * Mathf.Abs(normal.z);

        if (Mathf.Abs(d) > r) return false;

        return true;
    }
}
