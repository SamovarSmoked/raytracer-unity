// =============================================================================
// RayTracingObject.cs — Sphere component (Phase 3+4)
// Supports Lambertian, Metal, Dielectric material types
// =============================================================================
using UnityEngine;

public enum RayTracingMaterialType
{
    Lambertian  = 0,
    Metal       = 1,
    Dielectric  = 2
}

public class RayTracingObject : MonoBehaviour
{
    [Header("Geometry")]
    [SerializeField, Min(0.01f)] private float radius = 0.5f;

    [Header("Material")]
    [SerializeField] private RayTracingMaterialType materialType = RayTracingMaterialType.Lambertian;
    [SerializeField] private Color albedo = Color.white;
    [SerializeField, Range(0f, 1f)] private float specular = 0.1f;

    [Header("Metal")]
    [Tooltip("Roughness for metal surfaces. 0 = perfect mirror.")]
    [SerializeField, Range(0f, 1f)] private float fuzz = 0f;

    [Header("Dielectric (Glass)")]
    [Tooltip("Index of refraction. Glass ≈ 1.5, Water ≈ 1.33, Diamond ≈ 2.42")]
    [SerializeField, Min(1f)] private float ior = 1.5f;

    [Header("Emission")]
    [SerializeField] private Color emission = Color.black;
    [SerializeField, Min(0f)] private float emissionIntensity = 0f;

    // Public accessors (with setters for SphereGenerator)
    public float Radius                        { get => radius; set => radius = value; }
    public RayTracingMaterialType MaterialType { get => materialType; set => materialType = value; }
    public Color Albedo                        { get => albedo; set => albedo = value; }
    public float Specular                      { get => specular; set => specular = value; }
    public float Fuzz                          { get => fuzz; set => fuzz = value; }
    public float IOR                           { get => ior; set => ior = value; }
    public Color Emission                      { get => emission; set => emission = value; }
    public float EmissionIntensity             { get => emissionIntensity; set => emissionIntensity = value; }

    private void OnDrawGizmos()
    {
        Color c = materialType == RayTracingMaterialType.Dielectric
            ? new Color(0.8f, 0.9f, 1f, 0.15f)
            : new Color(albedo.r, albedo.g, albedo.b, 0.3f);
        Gizmos.color = c;
        Gizmos.DrawSphere(transform.position, radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
