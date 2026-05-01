// =============================================================================
// FreeCameraController.cs — FPS-style camera controller
//
// WASD — рух, Mouse — огляд, Scroll — швидкість
// Shift — прискорення, Space/Ctrl — вгору/вниз
// Attach to the Main Camera alongside RayTracingMaster.
// =============================================================================
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 3f;
    [SerializeField] private float scrollSpeedStep = 0.5f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float smoothTime = 0.05f;
    [SerializeField, Range(-90f, 0f)]  private float minPitch = -89f;
    [SerializeField, Range(0f, 90f)]   private float maxPitch = 89f;

    [Header("Controls")]
    [Tooltip("Hold this mouse button to look around.")]
    [SerializeField] private int lookMouseButton = 1; // 1 = right click

    private float _yaw;
    private float _pitch;
    private Vector2 _smoothVelocity;
    private Vector2 _currentRotation;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw   = euler.y;
        _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        _currentRotation = new Vector2(_pitch, _yaw);
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleScrollSpeed();
    }

    private void HandleLook()
    {
        if (!Input.GetMouseButton(lookMouseButton)) return;

        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        _yaw   += mx;
        _pitch -= my;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // Smooth damp
        Vector2 target = new Vector2(_pitch, _yaw);
        _currentRotation = Vector2.SmoothDamp(_currentRotation, target,
            ref _smoothVelocity, smoothTime);

        transform.rotation = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0f);
    }

    private void HandleMovement()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= sprintMultiplier;

        Vector3 dir = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.Space))        dir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl))  dir -= Vector3.up;

        if (dir.sqrMagnitude > 0.001f)
            transform.position += dir.normalized * speed * Time.deltaTime;
    }

    private void HandleScrollSpeed()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed + scroll * scrollSpeedStep * 10f);
        }
    }
}
