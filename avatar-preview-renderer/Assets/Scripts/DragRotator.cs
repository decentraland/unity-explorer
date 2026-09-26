using UnityEngine;

/// <summary>
/// Handles autorotation and inertia-based user rotation.
/// </summary>
public class DragRotator : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    [Header("Drag Settings")] [SerializeField]
    private float dragSpeed = 1f;

    [SerializeField] private float inertiaDamp = 0.95f;

    // Well short of a right angle: the fit measures a YAW-invariant radius, so a spin presents the
    // silhouette it zoomed for but a steep tilt overflows the view.
    [SerializeField] private float maxPitch = 30f;

    [Header("Auto-Rotate Settings")] [SerializeField]
    private float autoRotateSpeed = 20f;

    [SerializeField] private float autoRotateDelay = 2f;
    [SerializeField] private float returnSpeed = 2f;

    private float _horizontalVel;
    private float _verticalVel;
    private float _lastDragTime;
    private Quaternion _initialRotation;

    // Rebuilt from angles each frame rather than accumulated: composing world Y and X turns works roll
    // in, and a tilt held only as a quaternion cannot be clamped.
    private float _yaw;
    private float _pitch;

    public bool AllowVertical { get; set; } = true;
    public bool EnableAutoRotate { get; set; } = true;
    public float DragSpeed { get => dragSpeed; set => dragSpeed = value; }

    private Quaternion? _targetRotation;

    private void Awake()
    {
        _initialRotation = transform.rotation;
    }

    public void OnDrag(Vector2 drag, float deltaTime)
    {
        _horizontalVel += -drag.x * dragSpeed * deltaTime;
        _verticalVel += -drag.y * dragSpeed * deltaTime;
        _lastDragTime = Time.time;
    }

    private void Update()
    {
        var dt = Time.deltaTime;

        if (_targetRotation.HasValue)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, _targetRotation.Value, returnSpeed * dt);

            if (Quaternion.Angle(transform.rotation, _targetRotation.Value) < 1f)
            {
                _targetRotation = null;
            }

            return;
        }

        // Framerate-independent dampening
        _horizontalVel *= Mathf.Pow(inertiaDamp, dt);
        _verticalVel *= Mathf.Pow(inertiaDamp, dt);

        // Velocity rotation
        _yaw += _horizontalVel;

        if (AllowVertical)
        {
            var tilted = _pitch + _verticalVel;
            _pitch = Mathf.Clamp(tilted, -maxPitch, maxPitch);

            // Drop inertia at the stop, or a flick keeps "arriving" after the subject has visibly stopped.
            if (!Mathf.Approximately(_pitch, tilted)) _verticalVel = 0f;
        }

        // Auto rotation
        if (Time.time - _lastDragTime > autoRotateDelay && EnableAutoRotate)
        {
            // Levelling rides with auto-rotate only: a self-spinning view is presenting a canonical
            // framing, while one tilted by hand is being inspected and should not spring back.
            _pitch = Mathf.Lerp(_pitch, 0f, returnSpeed * dt);
            _yaw += autoRotateSpeed * dt;
        }

        // An idle page auto-rotates indefinitely; past a few million degrees the float step exceeds the
        // per-frame increment and the spin jitters, then stalls. A full turn is a no-op for AngleAxis.
        _yaw %= 360f;

        ApplyRotation();
    }

    // Pitch OUTSIDE yaw, or the tilt goes about the subject's own axis and reads as roll once side-on.
    private void ApplyRotation() =>
        transform.rotation = Quaternion.AngleAxis(_pitch, Vector3.right)
                             * Quaternion.AngleAxis(_yaw, Vector3.up)
                             * _initialRotation;

    public void LookAtCamera(bool smooth)
    {
        var direction = mainCamera.transform.position - transform.position;
        direction.y = 0f; // Ignore vertical difference
        var targetRotation = Quaternion.LookRotation(direction);

        _horizontalVel = 0;
        _verticalVel = 0;
        _lastDragTime = 0;

        // Carry the angles across, or the frame after the smooth lerp finishes snaps back to the yaw
        // they still held. `direction` is flattened, so the target is a pure yaw turn.
        _yaw = (targetRotation * Quaternion.Inverse(_initialRotation)).eulerAngles.y;
        _pitch = 0f;

        if (smooth)
        {
            _targetRotation = targetRotation;
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    public void ResetRotation()
    {
        _yaw = 0f;
        _pitch = 0f;
        _horizontalVel = 0;
        _verticalVel = 0;
        _lastDragTime = 0;

        // An in-flight LookAtCamera would otherwise keep lerping straight over the reset.
        _targetRotation = null;

        transform.rotation = _initialRotation;
    }
}