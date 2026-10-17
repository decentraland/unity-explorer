#if DCL_RENDER_SERVER
using UnityEngine;

public partial class DragRotator
{
    /// <summary>
    /// Turns the subject to an exact orientation and drops any inertia, so a still taken this frame
    /// shows the requested angle. Pitch is ignored when vertical rotation is not allowed, and clamped
    /// to the same limit a drag has.
    /// </summary>
    public void SetAngles(float yaw, float pitch)
    {
        _yaw = yaw % 360f;
        _pitch = AllowVertical ? Mathf.Clamp(pitch, -maxPitch, maxPitch) : 0f;
        _horizontalVel = 0f;
        _verticalVel = 0f;
        _targetRotation = null;

        ApplyRotation();
    }
}
#endif
