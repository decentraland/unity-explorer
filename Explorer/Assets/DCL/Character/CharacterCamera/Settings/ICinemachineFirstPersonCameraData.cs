using Cinemachine;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineFirstPersonCameraData
    {
        CinemachineVirtualCamera Camera { get; }

        /// <summary>
        ///     POV modules allows input to control camera's rotation
        /// </summary>
        CinemachinePOV POV { get; }

        CinemachineBasicMultiChannelPerlin Noise { get; }
    }
}
