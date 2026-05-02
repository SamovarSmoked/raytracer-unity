// =============================================================================
// RayTracingMeshObject.cs — Mesh component for GPU Path Tracer
// Inherits material properties from RayTracingMaterialBase.
// Caches child MeshFilters on enable and self-registers.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMeshObject : RayTracingMaterialBase
{
    // -- Registration ----------------------------------------------------------

    private static readonly HashSet<RayTracingMeshObject> _registered = new HashSet<RayTracingMeshObject>();
    public  static IReadOnlyCollection<RayTracingMeshObject> RegisteredObjects => _registered;

    /// <summary>Raised when any mesh object is added or removed (topology change).</summary>
    public static bool RegistrationChanged { get; set; }

    // -- Cached MeshFilters (avoids GetComponentsInChildren every frame) --------

    private MeshFilter[] _cachedFilters;
    public  MeshFilter[] CachedFilters => _cachedFilters;

    // -- Transform dirty tracking ----------------------------------------------

    private Matrix4x4 _lastLocalToWorld;

    /// <summary>
    /// Returns true if any child transform has moved since last check.
    /// Resets automatically after the call.
    /// </summary>
    public bool CheckTransformDirty()
    {
        Matrix4x4 current = transform.localToWorldMatrix;
        if (current != _lastLocalToWorld)
        {
            _lastLocalToWorld = current;
            return true;
        }
        return false;
    }

    // -- Lifecycle -------------------------------------------------------------

    private void OnEnable()
    {
        _cachedFilters = GetComponentsInChildren<MeshFilter>();
        _lastLocalToWorld = transform.localToWorldMatrix;
        _registered.Add(this);
        RegistrationChanged = true;
    }

    private void OnDisable()
    {
        _registered.Remove(this);
        RegistrationChanged = true;
        _cachedFilters = null;
    }

    // -- Gizmos ----------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>();
        if (mfs.Length == 0) return;

        Color c = MaterialType == RayTracingMaterialType.Dielectric
            ? new Color(0.8f, 0.9f, 1f, 0.15f)
            : new Color(Albedo.r, Albedo.g, Albedo.b, 0.2f);

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            Gizmos.color = c;
            Gizmos.matrix = mf.transform.localToWorldMatrix;
            Gizmos.DrawMesh(mf.sharedMesh);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireMesh(mf.sharedMesh);
        }
    }
}
