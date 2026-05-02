// =============================================================================
// RayTracingObject.cs — Sphere component for GPU Path Tracer
// Inherits material properties from RayTracingMaterialBase.
// Self-registers so RayTracingMaster never needs FindObjectsByType.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

public class RayTracingObject : RayTracingMaterialBase
{
    // -- Registration ----------------------------------------------------------

    private static readonly HashSet<RayTracingObject> _registered = new HashSet<RayTracingObject>();
    public  static IReadOnlyCollection<RayTracingObject> RegisteredObjects => _registered;

    /// <summary>Raised when any sphere is added or removed (topology change).</summary>
    public static bool RegistrationChanged { get; set; }

    private void OnEnable()
    {
        _registered.Add(this);
        RegistrationChanged = true;
    }

    private void OnDisable()
    {
        _registered.Remove(this);
        RegistrationChanged = true;
    }

    // -- Sphere-specific -------------------------------------------------------

    [Header("Geometry")]
    [SerializeField, Min(0.01f)] private float radius = 0.5f;

    public float Radius
    {
        get => radius;
        set { if (!Mathf.Approximately(radius, value)) { radius = value; IsDirty = true; } }
    }

    // -- Gizmos ----------------------------------------------------------------

    private void OnDrawGizmos()
    {
        Color c = MaterialType == RayTracingMaterialType.Dielectric
            ? new Color(0.8f, 0.9f, 1f, 0.15f)
            : new Color(Albedo.r, Albedo.g, Albedo.b, 0.3f);
        Gizmos.color = c;
        Gizmos.DrawSphere(transform.position, radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
