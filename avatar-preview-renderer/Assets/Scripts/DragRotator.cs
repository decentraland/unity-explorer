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

    // How far the subject can be tipped towards or away from the camera. Well short of a right angle,
    // because the framing is fitted once at load and never refitted: PreviewCameraController measures a
    // YAW-invariant radius, so a spin presents the same silhouette it zoomed for but a steep tilt does
    // not, and the subject starts to overflow the view.
    [SerializeField] private float maxPitch = 30f;

    [Header("Auto-Rotate Settings")] [SerializeField]
    private float autoRotateSpeed = 20f;

    [SerializeField] private float autoRotateDelay = 2f;
    [SerializeField] private float returnSpeed = 2f;

    private float _horizontalVel;
    private float _verticalVel;
    private float _lastDragTime;
    private Quaternion _initialRotation;

    // The rotation is rebuilt from these two every frame rather than accumulated onto the transform.
    // Turning about world Y and world X in sequence quietly works ROLL into the result - which is what
    // the old code was flattening Z for - and there is no way to clamp a tilt you are only holding as a
    // quaternion. Carrying the angles means roll cannot arise and the clamp is a Mathf.Clamp.
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

            // Drop the inertia at the stop rather than letting it keep pushing into it, which would
            // leave a flick still "arriving" for a second after the subject has visibly stopped.
            if (!Mathf.Approximately(_pitch, tilted)) _verticalVel = 0f;
        }

        // Auto rotation
        if (Time.time - _lastDragTime > autoRotateDelay && EnableAutoRotate)
        {
            // Levelling rides with auto-rotate on purpose. A view that spins by itself is presenting a
            // canonical framing, so it should ease back to level; one tilted by hand is being inspected,
            // and springing back would fight that. The tilt there is cleared by ResetRotation instead,
            // which both the view switch and a reload already call.
            _pitch = Mathf.Lerp(_pitch, 0f, returnSpeed * dt);
            _yaw += autoRotateSpeed * dt;
        }

        ApplyRotation();
    }

    // Pitch OUTSIDE yaw, so the tilt stays about the camera's horizontal axis however far the subject has
    // been spun. The other order would tip it about its own axis and read as a roll once it is side-on.
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

        // Carry the angles across as well, or the first frame after the smooth lerp finishes would snap
        // back to whatever yaw they still held. `direction` is flattened, so the target is a pure yaw
        // turn away from the initial rotation and the pitch goes to zero with it.
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

        // An in-flight LookAtCamera would otherwise keep lerping straight over the reset on the next
        // frame, leaving the subject somewhere neither call asked for.
        _targetRotation = null;

        transform.rotation = _initialRotation;
    }
}