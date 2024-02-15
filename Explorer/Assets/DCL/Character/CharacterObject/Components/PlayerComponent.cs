using UnityEngine;

namespace DCL.Character.Components
{
    /// <summary>
    ///     A special marker of the player entity
    /// </summary>
    public struct PlayerComponent
    {
        /// <summary>
        ///     Transform the camera will follow
        /// </summary>
        public readonly Transform CameraFocus;

        public PlayerComponent(Transform cameraFocus)
        {
            CameraFocus = cameraFocus;
        }
    }
}
