using Cinemachine;
using System.Collections.Generic;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineThirdPersonCameraData
    {
        /// <summary>
        ///     How much third person camera zoom changes on Wheel scroll
        /// </summary>
        float ZoomSensitivity { get; }

        /// <summary>
        ///     3 elements list with the orbit thresholds when the camera is a third person mode and zoomed in to the limit
        /// </summary>
        IReadOnlyList<CinemachineFreeLook.Orbit> ZoomInOrbitThreshold { get; }

        /// <summary>
        ///     3 elements list with the orbit thresholds when the camera is a third person mode and zoomed out to the limit
        /// </summary>
        IReadOnlyList<CinemachineFreeLook.Orbit> ZoomOutOrbitThreshold { get; }

        CinemachineFreeLook Camera { get; }
    }
}
