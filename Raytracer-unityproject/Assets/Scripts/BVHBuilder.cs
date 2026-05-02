// =============================================================================
// BVHBuilder.cs — Bounding Volume Hierarchy Builder for GPU Ray Tracer
// Uses a simple Midpoint/Longest-Axis split strategy.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

public class BVHBuilder
{
    public struct BVHNode
    {
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public int leftChild;
        public int rightChild;
        public int triStart;
        public int triCount;
    }

    public struct BuildTriangle
    {
        public TriangleData data;
        public Vector3 centroid;
        public Vector3 min;
        public Vector3 max;
    }

    private List<BVHNode> _nodes;
    private BuildTriangle[] _triangles;
    private int _nodeCount;

    public void Build(List<TriangleData> inputTriangles, out List<BVHNode> outNodes, out List<TriangleData> outTriangles)
    {
        int count = inputTriangles.Count;
        _triangles = new BuildTriangle[count];
        
        for (int i = 0; i < count; i++)
        {
            var t = inputTriangles[i];
            Vector3 min = Vector3.Min(t.v0, Vector3.Min(t.v1, t.v2));
            Vector3 max = Vector3.Max(t.v0, Vector3.Max(t.v1, t.v2));
            _triangles[i] = new BuildTriangle
            {
                data = t,
                centroid = (t.v0 + t.v1 + t.v2) * 0.3333333f,
                min = min,
                max = max
            };
        }

        // Pre-allocate enough nodes (max 2N - 1 for a binary tree)
        _nodes = new List<BVHNode>(new BVHNode[count * 2 + 1]);
        _nodeCount = 0;

        BuildRecursive(0, count);

        // Shrink node list to actual size
        outNodes = _nodes.GetRange(0, _nodeCount);
        
        // Output reordered triangles
        outTriangles = new List<TriangleData>(count);
        for (int i = 0; i < count; i++)
            outTriangles.Add(_triangles[i].data);
    }

    private int BuildRecursive(int triStart, int triCount)
    {
        int nodeIdx = _nodeCount++;
        BVHNode node = new BVHNode();
        node.triStart = triStart;
        node.triCount = triCount;
        node.leftChild = -1;
        node.rightChild = -1;

        // Calculate node bounds
        Vector3 bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        
        for (int i = 0; i < triCount; i++)
        {
            bMin = Vector3.Min(bMin, _triangles[triStart + i].min);
            bMax = Vector3.Max(bMax, _triangles[triStart + i].max);
        }
        node.boundsMin = bMin;
        node.boundsMax = bMax;

        // Leaf condition
        if (triCount <= 4)
        {
            _nodes[nodeIdx] = node;
            return nodeIdx;
        }

        // Find longest axis of the centroid bounding box
        Vector3 cMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 cMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < triCount; i++)
        {
            cMin = Vector3.Min(cMin, _triangles[triStart + i].centroid);
            cMax = Vector3.Max(cMax, _triangles[triStart + i].centroid);
        }

        Vector3 extent = cMax - cMin;
        int axis = 0;
        if (extent.y > extent.x) axis = 1;
        if (extent.z > extent[axis]) axis = 2;

        float splitPos = cMin[axis] + extent[axis] * 0.5f;

        // Partition triangles
        int i_left = triStart;
        int i_right = triStart + triCount - 1;

        while (i_left <= i_right)
        {
            if (_triangles[i_left].centroid[axis] < splitPos)
            {
                i_left++;
            }
            else
            {
                // Swap
                var temp = _triangles[i_left];
                _triangles[i_left] = _triangles[i_right];
                _triangles[i_right] = temp;
                i_right--;
            }
        }

        int leftCount = i_left - triStart;
        if (leftCount == 0 || leftCount == triCount)
        {
            // Fallback if midpoint split fails (all centroids at same spot)
            leftCount = triCount / 2;
        }

        // Recursive children
        node.triCount = 0; // It's an internal node now
        node.leftChild = BuildRecursive(triStart, leftCount);
        node.rightChild = BuildRecursive(triStart + leftCount, triCount - leftCount);

        _nodes[nodeIdx] = node;
        return nodeIdx;
    }
}
