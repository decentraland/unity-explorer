using Cinemachine;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineFreeCameraData
    {
        CinemachineVirtualCamera Camera { get; }

        CinemachinePOV POV { get; }

        float Speed { get; }

        /// <summary>
        ///     Default free camera position on switch relative to the player
        /// </summary>
        Vector3 DefaultPosition { get; }
    }
}
