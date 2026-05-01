// =============================================================================
// RayTracingMaster.cs — CPU-side controller for the GPU ray tracer
//
// Attach this script to the Main Camera. Assign the RayTracingShader in the
// Inspector. The script dispatches the compute shader every frame and blits
// the result to the screen via OnRenderImage.
//
// Phase 1: hardcoded scene, single frame (no accumulation yet).
// =============================================================================

using UnityEngine;

[ExecuteAlways]
[ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------

    [Header("Compute Shader")]
    [Tooltip("Assign the RayTracingShader.compute asset here.")]
    [SerializeField] private ComputeShader rayTracingShader;

    [Header("Sky")]
    [SerializeField] private Color skyColorHorizon = new Color(1.0f, 0.78f, 0.45f, 1.0f);
    [SerializeField] private Color skyColorZenith  = new Color(0.25f, 0.47f, 0.92f, 1.0f);

    [Header("Directional Light")]
    [Tooltip("Direction the light is coming FROM (will be normalized).")]
    [SerializeField] private Vector3 lightDirection = new Vector3(1.0f, 0.85f, -0.6f);

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private Camera  _camera;
    private RenderTexture _target;
    private int _kernelCSMain;
    private bool _kernelFound;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();

        if (rayTracingShader != null)
        {
            _kernelCSMain = rayTracingShader.FindKernel("CSMain");
            _kernelFound  = true;
        }
    }

    private void OnDisable()
    {
        ReleaseTarget();
    }

    // -----------------------------------------------------------------------
    // Render pipeline hook
    // -----------------------------------------------------------------------

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (rayTracingShader == null || !_kernelFound)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureRenderTarget();
        SetShaderParameters();

        // Dispatch — one thread per pixel, groups of 8×8
        int groupsX = Mathf.CeilToInt(Screen.width  / 8.0f);
        int groupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        rayTracingShader.SetTexture(_kernelCSMain, "Result", _target);
        rayTracingShader.Dispatch(_kernelCSMain, groupsX, groupsY, 1);

        // Blit result to screen
        Graphics.Blit(_target, destination);
    }

    // -----------------------------------------------------------------------
    // Render target management
    // -----------------------------------------------------------------------

    private void EnsureRenderTarget()
    {
        if (_target != null && _target.width == Screen.width && _target.height == Screen.height)
            return;

        ReleaseTarget();

        _target = new RenderTexture(Screen.width, Screen.height, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        _target.enableRandomWrite = true;
        _target.Create();
    }

    private void ReleaseTarget()
    {
        if (_target != null)
        {
            _target.Release();
            _target = null;
        }
    }

    // -----------------------------------------------------------------------
    // Shader parameters
    // -----------------------------------------------------------------------

    private void SetShaderParameters()
    {
        // Camera matrices
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Sky
        rayTracingShader.SetVector("_SkyColorHorizon", skyColorHorizon);
        rayTracingShader.SetVector("_SkyColorZenith",  skyColorZenith);

        // Directional light (normalized direction TO the light source)
        rayTracingShader.SetVector("_DirectionalLight", lightDirection.normalized);
    }
}
