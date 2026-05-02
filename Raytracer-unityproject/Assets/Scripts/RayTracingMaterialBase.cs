// =============================================================================
// RayTracingMaterialBase.cs — Shared material properties for all RT primitives
// Base class for RayTracingObject (spheres) and RayTracingMeshObject (meshes).
// Provides dirty-tracking so the buffer manager knows when to rebuild.
// =============================================================================
using UnityEngine;

public enum RayTracingMaterialType
{
    Lambertian  = 0,
    Metal       = 1,
    Dielectric  = 2
}

public abstract class RayTracingMaterialBase : MonoBehaviour
{
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

    /// <summary>
    /// Set to true when any material property changes.
    /// The buffer manager reads and clears this flag each frame.
    /// </summary>
    public bool IsDirty { get; set; } = true;

    // -- Public accessors (mark dirty on set) ----------------------------------

    public RayTracingMaterialType MaterialType
    {
        get => materialType;
        set { if (materialType != value) { materialType = value; IsDirty = true; } }
    }

    public Color Albedo
    {
        get => albedo;
        set { if (albedo != value) { albedo = value; IsDirty = true; } }
    }

    public float Specular
    {
        get => specular;
        set { if (!Mathf.Approximately(specular, value)) { specular = value; IsDirty = true; } }
    }

    public float Fuzz
    {
        get => fuzz;
        set { if (!Mathf.Approximately(fuzz, value)) { fuzz = value; IsDirty = true; } }
    }

    public float IOR
    {
        get => ior;
        set { if (!Mathf.Approximately(ior, value)) { ior = value; IsDirty = true; } }
    }

    public Color Emission
    {
        get => emission;
        set { if (emission != value) { emission = value; IsDirty = true; } }
    }

    public float EmissionIntensity
    {
        get => emissionIntensity;
        set { if (!Mathf.Approximately(emissionIntensity, value)) { emissionIntensity = value; IsDirty = true; } }
    }

#if UNITY_EDITOR
    // Detect Inspector changes in edit mode (SerializedFields bypass the setters)
    private void OnValidate() => IsDirty = true;
#endif
}
