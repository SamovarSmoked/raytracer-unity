// =============================================================================
// SceneBufferManager.cs — Builds and manages GPU ComputeBuffers for the scene
// Handles sphere, mesh/triangle, and BVH buffers.
// Uses dirty flags + registration instead of hashing + FindObjectsByType.
// =============================================================================
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class SceneBufferManager
{
    // -- Public state ----------------------------------------------------------

    private ComputeBuffer _sphereBuffer;
    public ComputeBuffer SphereBuffer => _sphereBuffer;

    private ComputeBuffer _triangleBuffer;
    public ComputeBuffer TriangleBuffer => _triangleBuffer;

    private ComputeBuffer _meshObjectBuffer;
    public ComputeBuffer MeshObjectBuffer => _meshObjectBuffer;

    private ComputeBuffer _bvhBuffer;
    public ComputeBuffer BVHBuffer => _bvhBuffer;

    public int SphereCount     { get; private set; }
    public int MeshObjectCount { get; private set; }
    public int TriangleCount   { get; private set; }

    /// <summary>True if any buffer was rebuilt this frame (caller should reset accumulation).</summary>
    public bool SceneChanged { get; private set; }

    // -- BLAS cache ------------------------------------------------------------

    private readonly Dictionary<Mesh, BLASData> _blasCache = new Dictionary<Mesh, BLASData>();

    // -- Reusable lists (avoid re-allocation each frame) -----------------------

    private readonly List<SphereData>     _sphereList   = new List<SphereData>();
    private readonly List<TriangleData>   _triangleList = new List<TriangleData>();
    private readonly List<BVHNodeData>    _bvhNodeList  = new List<BVHNodeData>();
    private readonly List<MeshObjectData> _meshDataList = new List<MeshObjectData>();

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>Call once per frame. Rebuilds only what changed.</summary>
    public void UpdateBuffers()
    {
        SceneChanged = false;
        UpdateSphereBuffer();
        UpdateMeshBuffers();
    }

    public void ReleaseAll()
    {
        ReleaseBuffer(ref _sphereBuffer);
        ReleaseBuffer(ref _triangleBuffer);
        ReleaseBuffer(ref _meshObjectBuffer);
        ReleaseBuffer(ref _bvhBuffer);
        _blasCache.Clear();
    }

    // =========================================================================
    // Sphere buffer
    // =========================================================================

    private void UpdateSphereBuffer()
    {
        var objects = RayTracingObject.RegisteredObjects;

        // Check if anything changed
        bool needRebuild = RayTracingObject.RegistrationChanged;
        if (!needRebuild)
        {
            foreach (var obj in objects)
            {
                if (obj.IsDirty || obj.transform.hasChanged)
                {
                    needRebuild = true;
                    break;
                }
            }
        }

        if (!needRebuild && SphereBuffer != null)
            return;

        // Clear dirty flags
        RayTracingObject.RegistrationChanged = false;
        foreach (var obj in objects)
        {
            obj.IsDirty = false;
            obj.transform.hasChanged = false;
        }

        SceneChanged = true;

        // Build data
        _sphereList.Clear();
        foreach (var obj in objects)
        {
            _sphereList.Add(new SphereData
            {
                position     = obj.transform.position,
                radius       = obj.Radius,
                albedo       = new Vector3(obj.Albedo.r, obj.Albedo.g, obj.Albedo.b),
                specular     = obj.Specular,
                emission     = new Vector3(obj.Emission.r, obj.Emission.g, obj.Emission.b)
                               * obj.EmissionIntensity,
                fuzz         = obj.Fuzz,
                ior          = obj.IOR,
                materialType = (int)obj.MaterialType
            });
        }

        // Upload
        ReallocateIfNeeded(ref _sphereBuffer, Mathf.Max(1, _sphereList.Count),
            Marshal.SizeOf<SphereData>());
        if (_sphereList.Count > 0)
            _sphereBuffer.SetData(_sphereList);

        SphereCount = _sphereList.Count;
    }

    // =========================================================================
    // Mesh / Triangle / BVH buffers
    // =========================================================================

    private void UpdateMeshBuffers()
    {
        var meshObjects = RayTracingMeshObject.RegisteredObjects;

        // --- Detect what changed ---
        bool topologyChanged  = RayTracingMeshObject.RegistrationChanged;
        bool transformChanged = false;

        foreach (var mobj in meshObjects)
        {
            if (mobj.IsDirty)
                transformChanged = true;

            if (mobj.CheckTransformDirty())
                transformChanged = true;
        }

        if (!topologyChanged && !transformChanged && MeshObjectBuffer != null)
            return;

        // Clear flags
        RayTracingMeshObject.RegistrationChanged = false;
        foreach (var mobj in meshObjects)
            mobj.IsDirty = false;

        SceneChanged = true;

        // --- Collect unique meshes ---
        var uniqueMeshes = new HashSet<Mesh>();
        foreach (var mobj in meshObjects)
        {
            if (mobj.CachedFilters == null) continue;
            foreach (var mf in mobj.CachedFilters)
            {
                if (mf.sharedMesh != null && mf.sharedMesh.isReadable)
                    uniqueMeshes.Add(mf.sharedMesh);
            }
        }

        // --- Rebuild BLAS if topology changed ---
        _triangleList.Clear();
        _bvhNodeList.Clear();

        if (topologyChanged)
        {
            _blasCache.Clear();

            foreach (Mesh mesh in uniqueMeshes)
            {
                BLASData blas = BuildBLAS(mesh);

                // Assign global offsets
                blas.globalTriOffset  = _triangleList.Count;
                blas.globalNodeOffset = _bvhNodeList.Count;
                _blasCache[mesh] = blas;

                _triangleList.AddRange(blas.triangles);

                // Flatten BVH nodes with global offsets
                foreach (var node in blas.nodes)
                {
                    _bvhNodeList.Add(new BVHNodeData
                    {
                        boundsMin  = node.boundsMin,
                        boundsMax  = node.boundsMax,
                        leftChild  = node.leftChild  > 0 ? node.leftChild  + blas.globalNodeOffset : -1,
                        rightChild = node.rightChild > 0 ? node.rightChild + blas.globalNodeOffset : -1,
                        triStart   = node.triStart + blas.globalTriOffset,
                        triCount   = node.triCount
                    });
                }
            }

            // Upload triangles & BVH
            ReleaseBuffer(ref _triangleBuffer);
            ReleaseBuffer(ref _bvhBuffer);

            int triCount = Mathf.Max(1, _triangleList.Count);
            _triangleBuffer = new ComputeBuffer(triCount, Marshal.SizeOf<TriangleData>());
            if (_triangleList.Count > 0) _triangleBuffer.SetData(_triangleList);

            int nodeCount = Mathf.Max(1, _bvhNodeList.Count);
            _bvhBuffer = new ComputeBuffer(nodeCount, Marshal.SizeOf<BVHNodeData>());
            if (_bvhNodeList.Count > 0) _bvhBuffer.SetData(_bvhNodeList);

            TriangleCount = _triangleList.Count;

#if UNITY_EDITOR
            Debug.Log($"[RayTracing] BLAS rebuilt: {TriangleCount} tris, {_bvhNodeList.Count} BVH nodes.");
#endif
        }

        // --- Build TLAS (always, transforms may have changed) ---
        _meshDataList.Clear();
        foreach (var mobj in meshObjects)
        {
            if (mobj.CachedFilters == null) continue;
            foreach (var mf in mobj.CachedFilters)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                if (!_blasCache.TryGetValue(mesh, out BLASData blas)) continue;

                _meshDataList.Add(new MeshObjectData
                {
                    worldToLocal       = mf.transform.worldToLocalMatrix,
                    localToWorldNormal = mf.transform.localToWorldMatrix.inverse.transpose,
                    bvhRootIndex       = blas.globalNodeOffset,
                    albedo             = new Vector3(mobj.Albedo.r, mobj.Albedo.g, mobj.Albedo.b),
                    specular           = mobj.Specular,
                    emission           = new Vector3(mobj.Emission.r, mobj.Emission.g, mobj.Emission.b)
                                         * mobj.EmissionIntensity,
                    fuzz               = mobj.Fuzz,
                    ior                = mobj.IOR,
                    materialType       = (int)mobj.MaterialType
                });
            }
        }

        ReallocateIfNeeded(ref _meshObjectBuffer, Mathf.Max(1, _meshDataList.Count),
            Marshal.SizeOf<MeshObjectData>());
        if (_meshDataList.Count > 0)
            _meshObjectBuffer.SetData(_meshDataList);

        MeshObjectCount = _meshDataList.Count;
    }

    // =========================================================================
    // BLAS builder
    // =========================================================================

    private BLASData BuildBLAS(Mesh mesh)
    {
        Vector3[] verts   = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[]     indices = mesh.triangles;

        var localTriangles = new List<TriangleData>(indices.Length / 3);
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = verts[indices[i]];
            Vector3 v1 = verts[indices[i + 1]];
            Vector3 v2 = verts[indices[i + 2]];

            Vector3 n0, n1, n2;
            if (normals != null && normals.Length > 0)
            {
                n0 = normals[indices[i]];
                n1 = normals[indices[i + 1]];
                n2 = normals[indices[i + 2]];
            }
            else
            {
                Vector3 flatN = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                n0 = n1 = n2 = flatN;
            }

            localTriangles.Add(new TriangleData
            {
                v0 = v0, v1 = v1, v2 = v2,
                n0 = n0, n1 = n1, n2 = n2
            });
        }

        var builder = new BVHBuilder();
        builder.Build(localTriangles, out var nodes, out var sortedTris);

        return new BLASData { triangles = sortedTris, nodes = nodes };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Reallocate a buffer only if size changed.</summary>
    private static void ReallocateIfNeeded(ref ComputeBuffer buffer, int count, int stride)
    {
        if (buffer != null && buffer.count == count)
            return;

        if (buffer != null) buffer.Release();
        buffer = new ComputeBuffer(count, stride);
    }

    private static void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        if (buffer != null) { buffer.Release(); buffer = null; }
    }
}
