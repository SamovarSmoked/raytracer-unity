// =============================================================================
// SphereGenerator.cs — Scene generator (Phase 3+4)
// Generates Lambertian, Metal, and Dielectric spheres
// =============================================================================
using UnityEngine;

public class SphereGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private int   sphereCount = 60;
    [SerializeField] private float areaRadius  = 12f;
    [SerializeField] private int   seed        = 42;

    [Header("Size")]
    [SerializeField] private float minRadius = 0.15f;
    [SerializeField] private float maxRadius = 0.6f;

    [Header("Showcase")]
    [SerializeField] private bool addShowcaseSpheres = true;

    [ContextMenu("Generate Scene")]
    public void GenerateScene()
    {
        ClearChildren();
        Random.State prev = Random.state;
        Random.InitState(seed);

        if (addShowcaseSpheres)
        {
            // Metal sphere
            CreateSphere("Metal_Center", new Vector3(0, 1, 0), 1.0f,
                RayTracingMaterialType.Metal,
                new Color(0.85f, 0.85f, 0.88f), 0.9f, 0.02f, 1.5f, Color.black, 0);

            // Glass sphere
            CreateSphere("Glass_Left", new Vector3(-2.5f, 0.8f, -0.3f), 0.8f,
                RayTracingMaterialType.Dielectric,
                new Color(0.95f, 0.95f, 1f), 0f, 0f, 1.52f, Color.black, 0);

            // Matte red
            CreateSphere("Matte_Right", new Vector3(2.3f, 0.7f, 0.5f), 0.7f,
                RayTracingMaterialType.Lambertian,
                new Color(0.85f, 0.1f, 0.08f), 0f, 0f, 1.5f, Color.black, 0);

            // Emissive
            CreateSphere("Emissive", new Vector3(0.5f, 0.4f, 2.0f), 0.4f,
                RayTracingMaterialType.Lambertian,
                new Color(1f, 0.8f, 0.3f), 0f, 0f, 1.5f,
                new Color(1f, 0.85f, 0.4f), 3f);
        }

        for (int i = 0; i < sphereCount; i++)
        {
            float r = Random.Range(minRadius, maxRadius);
            Vector3 pos = FindValidPosition(r);
            if (pos.y < 0) continue; // couldn't place

            float roll = Random.value;
            if (roll < 0.45f)
            {
                // Lambertian
                CreateSphere($"S_{i}", pos, r, RayTracingMaterialType.Lambertian,
                    RandomColor(), 0f, 0f, 1.5f, Color.black, 0);
            }
            else if (roll < 0.75f)
            {
                // Metal
                CreateSphere($"S_{i}", pos, r, RayTracingMaterialType.Metal,
                    RandomMetalColor(), Random.Range(0.5f, 0.95f),
                    Random.Range(0f, 0.3f), 1.5f, Color.black, 0);
            }
            else if (roll < 0.90f)
            {
                // Dielectric
                CreateSphere($"S_{i}", pos, r, RayTracingMaterialType.Dielectric,
                    new Color(1, 1, 1), 0f, 0f,
                    Random.Range(1.3f, 2.0f), Color.black, 0);
            }
            else
            {
                // Emissive
                Color ec = RandomColor();
                CreateSphere($"S_{i}", pos, r, RayTracingMaterialType.Lambertian,
                    ec, 0f, 0f, 1.5f, ec, Random.Range(1.5f, 5f));
            }
        }

        Random.state = prev;

#if UNITY_EDITOR
        Debug.Log($"[SphereGenerator] Created {transform.childCount} spheres.");
#endif
    }

    [ContextMenu("Clear Scene")]
    public void ClearChildren()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);
    }

    private void Start()
    {
        if (transform.childCount == 0) GenerateScene();
    }

    private Vector3 FindValidPosition(float r)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(2.5f, areaRadius);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, r, Mathf.Sin(angle) * dist);

            bool valid = true;
            foreach (Transform child in transform)
            {
                var rto = child.GetComponent<RayTracingObject>();
                if (rto != null && Vector3.Distance(pos, child.position) < r + rto.Radius + 0.1f)
                { valid = false; break; }
            }
            if (valid) return pos;
        }
        return Vector3.down; // failed
    }

    private void CreateSphere(string name, Vector3 pos, float radius,
        RayTracingMaterialType matType, Color albedo, float specular, float fuzz,
        float ior, Color emission, float emissionIntensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = pos;
        var rto = go.AddComponent<RayTracingObject>();

        // Direct property assignment (no SerializedObject — avoids silent failures)
        rto.Radius            = radius;
        rto.MaterialType      = matType;
        rto.Albedo            = albedo;
        rto.Specular          = specular;
        rto.Fuzz              = fuzz;
        rto.IOR               = ior;
        rto.Emission          = emission;
        rto.EmissionIntensity = emissionIntensity;
    }

    private Color RandomColor()
    {
        return Color.HSVToRGB(Random.value, Random.Range(0.5f, 1f), Random.Range(0.4f, 0.95f));
    }

    private Color RandomMetalColor()
    {
        float b = Random.Range(0.6f, 0.95f);
        float t = Random.Range(0f, 0.12f);
        return new Color(b + Random.Range(-t, t), b + Random.Range(-t, t), b + Random.Range(-t, t));
    }
}
