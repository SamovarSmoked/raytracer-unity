// =============================================================================
// RayTracingGPUStructs.cs — GPU-side data structures
// These structs are uploaded to ComputeBuffers and must match the HLSL layout
// exactly (field order, sizes, alignment via StructLayout.Sequential).
// =============================================================================
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// GPU struct for analytic spheres.
/// HLSL counterpart: struct Sphere (RayTracingShader.compute)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SphereData
{
    public Vector3 position;
    public float   radius;
    public Vector3 albedo;
    public float   specular;
    public Vector3 emission;
    public float   fuzz;
    public float   ior;
    public int     materialType;
}

/// <summary>
/// GPU struct for mesh triangles (local-space vertices + normals).
/// HLSL counterpart: struct Triangle (RayTracingShader.compute)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TriangleData
{
    public Vector3 v0, v1, v2;
    public Vector3 n0, n1, n2;
}

/// <summary>
/// GPU struct for a mesh instance in the TLAS.
/// HLSL counterpart: struct MeshObject (RayTracingShader.compute)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshObjectData
{
    public Matrix4x4 worldToLocal;
    public Matrix4x4 localToWorldNormal;
    public int       bvhRootIndex;
    public Vector3   albedo;
    public float     specular;
    public Vector3   emission;
    public float     fuzz;
    public float     ior;
    public int       materialType;
}

/// <summary>
/// GPU struct for a BVH tree node (leaf or internal).
/// HLSL counterpart: struct BVHNode (RayTracingShader.compute)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BVHNodeData
{
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public int     leftChild;
    public int     rightChild;
    public int     triStart;
    public int     triCount;
}

/// <summary>
/// CPU-side cache entry for a per-Mesh BLAS (Bottom-Level Acceleration Structure).
/// Stores the built BVH and reordered triangles so we only rebuild when topology changes.
/// </summary>
public struct BLASData
{
    public List<TriangleData>        triangles;
    public List<BVHBuilder.BVHNode> nodes;
    public int                       globalTriOffset;
    public int                       globalNodeOffset;
}
