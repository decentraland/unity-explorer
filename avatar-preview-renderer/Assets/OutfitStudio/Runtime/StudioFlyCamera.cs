using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutfitStudio
{
    /// <summary>
    /// Scene-view-style free-fly camera: hold the right mouse button to look around and move with
    /// WASD/QE (Shift to go faster). Added/removed on the studio's live camera by
    /// StudioFlyCameraController (Editor), play-mode + studio-scene gated — never present in a build
    /// or in Main.unity.
    ///
    /// The camera is normally driven every frame by a Cinemachine vcam through CinemachineBrain, which
    /// would otherwise immediately overwrite any transform we write here. While RMB is held we disable
    /// the brain so our writes stick; we deliberately do NOT re-enable it on release (that would snap
    /// the camera back to the vcam's authored framing mid-fly, which isn't what "fly around and stay
    /// there" means) — call <see cref="ReleaseToCinemachine"/> explicitly (e.g. a "Reset View" button)
    /// to hand framing back to Cinemachine.
    ///
    /// Look and move both target an instantaneous value from raw input each frame, then ease the
    /// applied yaw/pitch/velocity toward that target with a frame-rate-independent exponential lerp
    /// (`1 - e^(-dt/smoothTime)`) rather than snapping straight to it — raw per-frame deltas in the
    /// editor's Update() are uneven (Play mode ticks aren't locked to a fixed cadence like the display
    /// refresh), which reads as choppiness, especially moving the mouse and holding WASD at once.
    /// </summary>
    public class StudioFlyCamera : MonoBehaviour
    {
        public float MoveSpeed = 5f;
        public float LookSpeed = 0.15f;
        public float FastMultiplier = 3f;

        private const float MOVE_SMOOTH_TIME = 0.12f; // higher = more glide/inertia on start-stop
        private const float LOOK_SMOOTH_TIME = 0.05f; // kept low so look doesn't feel laggy, just deburred

        private float _yaw, _yawTarget;
        private float _pitch, _pitchTarget;
        private Vector3 _velocity; // current eased world-space velocity
        private bool _wasFlying;
        private CinemachineBrain _brain;

        private void Awake() => _brain = GetComponent<CinemachineBrain>();

        private void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            var flying = mouse.rightButton.isPressed;
            if (flying && !_wasFlying)
            {
                // Starting a fly session: resync yaw/pitch to whatever Cinemachine had framed right
                // now (before we take over), so the first mouse-delta frame doesn't snap the view to
                // a stale angle left over from the previous session.
                var e = transform.eulerAngles;
                _pitch = _pitchTarget = e.x > 180f ? e.x - 360f : e.x;
                _yaw = _yawTarget = e.y;
                _velocity = Vector3.zero;
                if (_brain != null) _brain.enabled = false;
            }
            _wasFlying = flying;

            if (!flying) return;

            var dt = Time.unscaledDeltaTime;

            var look = mouse.delta.ReadValue();
            _yawTarget += look.x * LookSpeed;
            _pitchTarget = Mathf.Clamp(_pitchTarget - look.y * LookSpeed, -89f, 89f);
            var lookT = 1f - Mathf.Exp(-dt / LOOK_SMOOTH_TIME);
            _yaw = Mathf.LerpAngle(_yaw, _yawTarget, lookT);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, lookT);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = Vector3.zero;
            if (kb.wKey.isPressed) move += Vector3.forward;
            if (kb.sKey.isPressed) move += Vector3.back;
            if (kb.aKey.isPressed) move += Vector3.left;
            if (kb.dKey.isPressed) move += Vector3.right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move += Vector3.down;

            var speed = MoveSpeed * (kb.leftShiftKey.isPressed ? FastMultiplier : 1f);
            var targetVelocity = move.sqrMagnitude > 0f
                ? transform.TransformDirection(move.normalized) * speed
                : Vector3.zero;
            var moveT = 1f - Mathf.Exp(-dt / MOVE_SMOOTH_TIME);
            _velocity = Vector3.Lerp(_velocity, targetVelocity, moveT);
            if (_velocity.sqrMagnitude > 1e-6f) transform.position += _velocity * dt;
        }

        /// <summary>Hands framing back to Cinemachine (e.g. a "Reset View" button). The next RMB press
        /// re-syncs yaw/pitch from the (now vcam-driven) transform, so this doesn't need to reset any
        /// state itself.</summary>
        public void ReleaseToCinemachine()
        {
            if (_brain != null) _brain.enabled = true;
        }

        private void OnDisable() => ReleaseToCinemachine();
    }
}
