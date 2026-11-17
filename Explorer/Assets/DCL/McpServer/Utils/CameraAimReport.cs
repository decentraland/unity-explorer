using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     The camera-aim fields <c>look_at</c> and <c>move_to</c> share, so both tools report an aim the same way:
    ///     the camera rotation, the residual angle to the target, and a warning when the rig stopped short of it.
    /// </summary>
    public static class CameraAimReport
    {
        /// <summary>Residual aim above which the camera visibly did not reach the point and the driver must be told.</summary>
        public const float RESIDUAL_AIM_WARNING_DEGREES = 2f;

        public const string RESIDUAL_WARNING =
            "the camera stopped short of the point (a rig limit, e.g. the third-person pitch clamp): "
            + "back away so the target sits at a shallower angle, or use set_camera_mode first_person / set_camera_pose";

        /// <summary>
        ///     Writes <c>cameraRotationEuler</c>, <c>aimErrorDegrees</c> and, above the threshold, <c>warning</c>.
        ///     Returns the residual angle in degrees.
        /// </summary>
        public static float Append(JObject result, Vector3 cameraPosition, Quaternion cameraRotation, Vector3 target)
        {
            float aimErrorDegrees = Vector3.Angle(cameraRotation * Vector3.forward, target - cameraPosition);

            result["cameraRotationEuler"] = cameraRotation.eulerAngles.ToVector();
            result["aimErrorDegrees"] = Math.Round(aimErrorDegrees, 1);

            if (aimErrorDegrees > RESIDUAL_AIM_WARNING_DEGREES)
                result["warning"] = RESIDUAL_WARNING;

            return aimErrorDegrees;
        }
    }
}
