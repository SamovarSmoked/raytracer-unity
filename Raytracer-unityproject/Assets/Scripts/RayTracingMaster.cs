// =============================================================================
// RayTracingMaster.cs — Path Tracer Controller
// Manages render targets, dispatches compute kernels, binds shader parameters.
// Scene buffer management is delegated to SceneBufferManager.
// =============================================================================
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
    [SerializeField, Range(0.01f, 5.0f)] private float denoiserStrength = 1.0f;
    [Tooltip("Disable denoiser after this many accumulated frames (0 = always on).")]
    [SerializeField, Range(0, 64)] private int denoiserFadeFrames = 20;
    [Tooltip("Normal similarity weight. Higher = stricter normal check.")]
    [SerializeField, Range(1f, 100f)] private float normalSigma = 32.0f;
    [Tooltip("Depth similarity weight. Higher = stricter depth check.")]
    [SerializeField, Range(0.01f, 10f)] private float depthSigma = 1.0f;

    // -- Private state ---------------------------------------------------------

    private Camera             _camera;
    private RenderTexture      _target;
    private RenderTexture      _accumulated;
    private RenderTexture      _filtered;
    private RenderTexture      _normalDepth;
    private SceneBufferManager _bufferManager;
    private int                _kernelCSMain;
    private int                _kernelCSFilter;
    private bool               _kernelFound;
    private uint               _frameIndex;
    private Matrix4x4          _lastCameraMatrix;
    private GUIStyle           _guiStyle;

    // =========================================================================
    // Lifecycle
    // =========================================================================

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _bufferManager = new SceneBufferManager();

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
        ReleaseRenderTargets();
        _bufferManager?.ReleaseAll();
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

    // =========================================================================
    // Rendering
    // =========================================================================

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (rayTracingShader == null || !_kernelFound)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureRenderTargets();
        _bufferManager.UpdateBuffers();

        if (_bufferManager.SceneChanged)
            _frameIndex = 0;

        _frameIndex++;
        BindShaderParameters();

        // Dispatch path tracer
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
            float adaptiveStrength = denoiserStrength;
            if (denoiserFadeFrames > 0)
                adaptiveStrength *= Mathf.Clamp01(1.0f - (float)_frameIndex / denoiserFadeFrames);

            rayTracingShader.SetFloat("_DenoiserStrength", Mathf.Max(adaptiveStrength, 0.01f));
            rayTracingShader.SetFloat("_NormalSigma", normalSigma);
            rayTracingShader.SetFloat("_DepthSigma", depthSigma);
            rayTracingShader.SetTexture(_kernelCSFilter, "Result", _target);
            rayTracingShader.SetTexture(_kernelCSFilter, "_Filtered", _filtered);
            rayTracingShader.SetTexture(_kernelCSFilter, "_NormalDepthMap", _normalDepth);
            rayTracingShader.Dispatch(_kernelCSFilter, gx, gy, 1);

            Graphics.Blit(_filtered, destination);
        }
        else
        {
            Graphics.Blit(_target, destination);
        }
    }

    // =========================================================================
    // UI: show sample count
    // =========================================================================

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        if (_guiStyle == null)
        {
            _guiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white }
            };
        }

        GUI.Label(new Rect(10, 10, 300, 30), $"Samples: {_frameIndex}", _guiStyle);
    }

    // =========================================================================
    // Render targets
    // =========================================================================

    private void EnsureRenderTargets()
    {
        if (_target != null && _target.width == Screen.width && _target.height == Screen.height)
            return;

        ReleaseRenderTargets();

        _target      = CreateRT();
        _accumulated = CreateRT();
        _filtered    = CreateRT();
        _normalDepth = CreateRT();
        _frameIndex  = 0;
    }

    private static RenderTexture CreateRT()
    {
        var rt = new RenderTexture(Screen.width, Screen.height, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void ReleaseRenderTargets()
    {
        ReleaseRT(ref _target);
        ReleaseRT(ref _accumulated);
        ReleaseRT(ref _filtered);
        ReleaseRT(ref _normalDepth);
    }

    private static void ReleaseRT(ref RenderTexture rt)
    {
        if (rt != null) { rt.Release(); rt = null; }
    }

    // =========================================================================
    // Shader parameter binding
    // =========================================================================

    private void BindShaderParameters()
    {
        // Camera
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Environment
        rayTracingShader.SetVector("_SkyColorHorizon", skyColorHorizon);
        rayTracingShader.SetVector("_SkyColorZenith", skyColorZenith);
        rayTracingShader.SetVector("_DirectionalLight", lightDirection.normalized);
        rayTracingShader.SetVector("_GroundColorA", groundColorA);
        rayTracingShader.SetVector("_GroundColorB", groundColorB);
        rayTracingShader.SetFloat("_GroundSpecular", groundSpecular);

        // Frame & quality
        rayTracingShader.SetInt("_FrameIndex", (int)_frameIndex);
        rayTracingShader.SetInt("_SamplesPerPixel", samplesPerPixel);

        // G-Buffer
        if (_normalDepth != null)
            rayTracingShader.SetTexture(_kernelCSMain, "_NormalDepthMap", _normalDepth);

        // Sphere buffer
        var sphereBuf = _bufferManager.SphereBuffer;
        if (sphereBuf != null)
        {
            rayTracingShader.SetBuffer(_kernelCSMain, "_Spheres", sphereBuf);
            rayTracingShader.SetInt("_SphereCount", _bufferManager.SphereCount);
        }

        // Mesh buffers
        var triBuf  = _bufferManager.TriangleBuffer;
        var meshBuf = _bufferManager.MeshObjectBuffer;
        var bvhBuf  = _bufferManager.BVHBuffer;
        if (triBuf != null && meshBuf != null && bvhBuf != null)
        {
            rayTracingShader.SetBuffer(_kernelCSMain, "_Triangles",   triBuf);
            rayTracingShader.SetBuffer(_kernelCSMain, "_MeshObjects", meshBuf);
            rayTracingShader.SetBuffer(_kernelCSMain, "_BVHNodes",    bvhBuf);
        }
        rayTracingShader.SetInt("_MeshCount", _bufferManager.MeshObjectCount);
    }
}
