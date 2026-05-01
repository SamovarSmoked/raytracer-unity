// =============================================================================
// RayTracingMaster.cs — Path Tracer Controller (Phase 3+4)
// Progressive accumulation, dual RenderTexture, frame tracking
// =============================================================================
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
[ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader rayTracingShader;

    [Header("Sky")]
    [SerializeField] private Color skyColorHorizon = new Color(1.0f, 0.78f, 0.45f);
    [SerializeField] private Color skyColorZenith  = new Color(0.25f, 0.47f, 0.92f);

    [Header("Directional Light")]
    [SerializeField] private Vector3 lightDirection = new Vector3(1.0f, 0.85f, -0.6f);

    [Header("Ground")]
    [SerializeField] private Color groundColorA = new Color(0.35f, 0.35f, 0.38f);
    [SerializeField] private Color groundColorB = new Color(0.75f, 0.75f, 0.78f);
    [SerializeField, Range(0, 1)] private float groundSpecular = 0.03f;

    [Header("Quality")]
    [Tooltip("Samples per pixel per frame. Higher = less noise, lower FPS.")]
    [SerializeField, Range(1, 16)] private int samplesPerPixel = 4;

    [Header("Denoiser")]
    [SerializeField] private bool enableDenoiser = true;
    [Tooltip("Filter strength. Higher = smoother but may lose detail.")]
    [SerializeField, Range(0.01f, 0.5f)] private float denoiserStrength = 0.15f;
    [Tooltip("Disable denoiser after this many accumulated frames (0 = always on).")]
    [SerializeField, Range(0, 64)] private int denoiserFadeFrames = 16;

    // GPU struct — must match HLSL exactly
    [StructLayout(LayoutKind.Sequential)]
    private struct SphereData
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

    [StructLayout(LayoutKind.Sequential)]
    private struct TriangleData
    {
        public Vector3 v0, v1, v2;
        public Vector3 n0, n1, n2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MeshObjectData
    {
        public int     triStart;
        public int     triCount;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public Vector3 albedo;
        public float   specular;
        public Vector3 emission;
        public float   fuzz;
        public float   ior;
        public int     materialType;
    }

    private Camera        _camera;
    private RenderTexture _target;
    private RenderTexture _accumulated;
    private RenderTexture _filtered;
    private ComputeBuffer _sphereBuffer;
    private ComputeBuffer _triangleBuffer;
    private ComputeBuffer _meshObjectBuffer;
    private int           _kernelCSMain;
    private int           _kernelCSFilter;
    private bool          _kernelFound;
    private uint          _frameIndex;
    private int           _lastSphereHash;
    private int           _lastMeshHash;
    private int           _actualMeshCount;
    private int           _actualTriCount;
    private Matrix4x4     _lastCameraMatrix;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        if (rayTracingShader != null)
        {
            _kernelCSMain   = rayTracingShader.FindKernel("CSMain");
            _kernelCSFilter = rayTracingShader.FindKernel("CSFilter");
            _kernelFound    = true;
        }
        _frameIndex = 0;
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    private void Update()
    {
        // Reset accumulation if camera moved
        if (_camera.cameraToWorldMatrix != _lastCameraMatrix)
        {
            _frameIndex = 0;
            _lastCameraMatrix = _camera.cameraToWorldMatrix;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (rayTracingShader == null || !_kernelFound)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureRenderTargets();
        RebuildSphereBuffer();
        RebuildMeshBuffer();
        _frameIndex++;
        SetShaderParameters();

        int gx = Mathf.CeilToInt(Screen.width  / 8.0f);
        int gy = Mathf.CeilToInt(Screen.height / 8.0f);
        rayTracingShader.SetTexture(_kernelCSMain, "_Accumulated", _accumulated);
        rayTracingShader.SetTexture(_kernelCSMain, "Result", _target);
        rayTracingShader.Dispatch(_kernelCSMain, gx, gy, 1);

        // Denoiser pass (adaptive: fades off as samples accumulate)
        bool applyDenoiser = enableDenoiser &&
            (denoiserFadeFrames == 0 || _frameIndex <= (uint)denoiserFadeFrames);

        if (applyDenoiser)
        {
            // Adaptive strength: full at frame 1, fading toward converged
            float adaptiveStrength = denoiserStrength;
            if (denoiserFadeFrames > 0)
                adaptiveStrength *= Mathf.Clamp01(1.0f - (float)_frameIndex / denoiserFadeFrames);

            rayTracingShader.SetFloat("_DenoiserStrength", Mathf.Max(adaptiveStrength, 0.01f));
            rayTracingShader.SetTexture(_kernelCSFilter, "Result", _target);
            rayTracingShader.SetTexture(_kernelCSFilter, "_Filtered", _filtered);
            rayTracingShader.Dispatch(_kernelCSFilter, gx, gy, 1);

            Graphics.Blit(_filtered, destination);
        }
        else
        {
            Graphics.Blit(_target, destination);
        }
    }

    // ---- UI: show sample count ----
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(10, 10, 300, 30), $"Samples: {_frameIndex}", style);
    }

    // ---- Render targets ----
    private void EnsureRenderTargets()
    {
        if (_target != null && _target.width == Screen.width && _target.height == Screen.height)
            return;

        ReleaseRenderTargets();

        _target      = CreateRT();
        _accumulated = CreateRT();
        _filtered    = CreateRT();
        _frameIndex  = 0;
    }

    private RenderTexture CreateRT()
    {
        var rt = new RenderTexture(Screen.width, Screen.height, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void ReleaseRenderTargets()
    {
        if (_target != null)      { _target.Release();      _target = null; }
        if (_accumulated != null)  { _accumulated.Release();  _accumulated = null; }
        if (_filtered != null)     { _filtered.Release();     _filtered = null; }
    }

    // ---- Sphere buffer ----
    private void RebuildSphereBuffer()
    {
        RayTracingObject[] objects = FindObjectsByType<RayTracingObject>(FindObjectsInactive.Exclude);
        int hash = ComputeHash(objects);
        if (hash == _lastSphereHash && _sphereBuffer != null) return;

        _lastSphereHash = hash;
        _frameIndex = 0; // scene changed, reset accumulation
        ReleaseSphereBuffer();

        if (objects.Length == 0)
        {
            _sphereBuffer = new ComputeBuffer(1, Marshal.SizeOf<SphereData>());
            return;
        }

        var data = new SphereData[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            data[i] = new SphereData
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
            };
        }

        _sphereBuffer = new ComputeBuffer(data.Length, Marshal.SizeOf<SphereData>());
        _sphereBuffer.SetData(data);
    }

    private int ComputeHash(RayTracingObject[] objects)
    {
        int h = objects.Length;
        for (int i = 0; i < objects.Length; i++)
        {
            h = h * 31 + objects[i].transform.position.GetHashCode();
            h = h * 31 + objects[i].Radius.GetHashCode();
            h = h * 31 + objects[i].Albedo.GetHashCode();
            h = h * 31 + ((int)objects[i].MaterialType).GetHashCode();
            h = h * 31 + objects[i].Fuzz.GetHashCode();
            h = h * 31 + objects[i].IOR.GetHashCode();
        }
        return h;
    }

    private void ReleaseSphereBuffer()
    {
        if (_sphereBuffer != null) { _sphereBuffer.Release(); _sphereBuffer = null; }
    }

    private void ReleaseAll()
    {
        ReleaseRenderTargets();
        ReleaseSphereBuffer();
        ReleaseMeshBuffers();
    }

    // ---- Mesh buffer ----
    private void RebuildMeshBuffer()
    {
        RayTracingMeshObject[] meshObjects = FindObjectsByType<RayTracingMeshObject>(FindObjectsInactive.Exclude);
        int hash = ComputeMeshHash(meshObjects);
        if (hash == _lastMeshHash && _meshObjectBuffer != null) return;

        _lastMeshHash = hash;
        _frameIndex = 0;
        ReleaseMeshBuffers();

        if (meshObjects.Length == 0)
        {
            _triangleBuffer   = new ComputeBuffer(1, Marshal.SizeOf<TriangleData>());
            _meshObjectBuffer = new ComputeBuffer(1, Marshal.SizeOf<MeshObjectData>());
            _actualMeshCount = 0;
            _actualTriCount = 0;
            return;
        }

        var allTriangles  = new System.Collections.Generic.List<TriangleData>();
        var allMeshData   = new System.Collections.Generic.List<MeshObjectData>();

        foreach (var mobj in meshObjects)
        {
            MeshFilter mf = mobj.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Vector3[] verts   = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[]     indices = mesh.triangles;
            Matrix4x4 ltw = mobj.transform.localToWorldMatrix;
            // Normal matrix: inverse transpose of upper-left 3x3
            Matrix4x4 normalMatrix = ltw.inverse.transpose;

            int triStart = allTriangles.Count;
            int triCount = indices.Length / 3;

            // Pre-compute world-space bounds
            Vector3 bmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 bmax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 wv0 = ltw.MultiplyPoint3x4(verts[indices[i]]);
                Vector3 wv1 = ltw.MultiplyPoint3x4(verts[indices[i + 1]]);
                Vector3 wv2 = ltw.MultiplyPoint3x4(verts[indices[i + 2]]);

                Vector3 wn0, wn1, wn2;
                if (normals != null && normals.Length > 0)
                {
                    wn0 = normalMatrix.MultiplyVector(normals[indices[i]]).normalized;
                    wn1 = normalMatrix.MultiplyVector(normals[indices[i + 1]]).normalized;
                    wn2 = normalMatrix.MultiplyVector(normals[indices[i + 2]]).normalized;
                }
                else
                {
                    // Fallback: flat normal from triangle edges
                    Vector3 flatN = Vector3.Cross(wv1 - wv0, wv2 - wv0).normalized;
                    wn0 = wn1 = wn2 = flatN;
                }

                allTriangles.Add(new TriangleData
                {
                    v0 = wv0, v1 = wv1, v2 = wv2,
                    n0 = wn0, n1 = wn1, n2 = wn2
                });

                // Expand AABB
                bmin = Vector3.Min(bmin, Vector3.Min(wv0, Vector3.Min(wv1, wv2)));
                bmax = Vector3.Max(bmax, Vector3.Max(wv0, Vector3.Max(wv1, wv2)));
            }

            allMeshData.Add(new MeshObjectData
            {
                triStart     = triStart,
                triCount     = triCount,
                boundsMin    = bmin,
                boundsMax    = bmax,
                albedo       = new Vector3(mobj.Albedo.r, mobj.Albedo.g, mobj.Albedo.b),
                specular     = mobj.Specular,
                emission     = new Vector3(mobj.Emission.r, mobj.Emission.g, mobj.Emission.b)
                               * mobj.EmissionIntensity,
                fuzz         = mobj.Fuzz,
                ior          = mobj.IOR,
                materialType = (int)mobj.MaterialType
            });
        }

        if (allTriangles.Count == 0)
        {
            _triangleBuffer   = new ComputeBuffer(1, Marshal.SizeOf<TriangleData>());
            _meshObjectBuffer = new ComputeBuffer(1, Marshal.SizeOf<MeshObjectData>());
            _actualMeshCount = 0;
            _actualTriCount = 0;
            return;
        }

        _triangleBuffer = new ComputeBuffer(allTriangles.Count, Marshal.SizeOf<TriangleData>());
        _triangleBuffer.SetData(allTriangles);

        _meshObjectBuffer = new ComputeBuffer(allMeshData.Count, Marshal.SizeOf<MeshObjectData>());
        _meshObjectBuffer.SetData(allMeshData);

        _actualMeshCount = allMeshData.Count;
        _actualTriCount  = allTriangles.Count;

        Debug.Log($"[RayTracing] Mesh buffer: {_actualMeshCount} objects, {_actualTriCount} triangles");
    }

    private int ComputeMeshHash(RayTracingMeshObject[] objects)
    {
        int h = objects.Length;
        for (int i = 0; i < objects.Length; i++)
        {
            h = h * 31 + objects[i].transform.localToWorldMatrix.GetHashCode();
            h = h * 31 + objects[i].Albedo.GetHashCode();
            h = h * 31 + ((int)objects[i].MaterialType).GetHashCode();
            MeshFilter mf = objects[i].GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                h = h * 31 + mf.sharedMesh.GetHashCode();
        }
        return h;
    }

    private void ReleaseMeshBuffers()
    {
        if (_triangleBuffer != null)   { _triangleBuffer.Release();   _triangleBuffer = null; }
        if (_meshObjectBuffer != null) { _meshObjectBuffer.Release(); _meshObjectBuffer = null; }
    }

    // ---- Shader params ----
    private void SetShaderParameters()
    {
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetVector("_SkyColorHorizon", skyColorHorizon);
        rayTracingShader.SetVector("_SkyColorZenith", skyColorZenith);
        rayTracingShader.SetVector("_DirectionalLight", lightDirection.normalized);
        rayTracingShader.SetVector("_GroundColorA", groundColorA);
        rayTracingShader.SetVector("_GroundColorB", groundColorB);
        rayTracingShader.SetFloat("_GroundSpecular", groundSpecular);
        rayTracingShader.SetInt("_FrameIndex", (int)_frameIndex);
        rayTracingShader.SetBuffer(_kernelCSMain, "_Spheres", _sphereBuffer);
        rayTracingShader.SetInt("_SphereCount", _sphereBuffer != null ? _sphereBuffer.count : 0);
        rayTracingShader.SetInt("_SamplesPerPixel", samplesPerPixel);

        // Mesh buffers
        if (_triangleBuffer != null && _meshObjectBuffer != null)
        {
            rayTracingShader.SetBuffer(_kernelCSMain, "_Triangles", _triangleBuffer);
            rayTracingShader.SetBuffer(_kernelCSMain, "_MeshObjects", _meshObjectBuffer);
        }
        rayTracingShader.SetInt("_MeshCount", _actualMeshCount);
        rayTracingShader.SetInt("_TriangleCount", _actualTriCount);
    }
}
