using Cinemachine;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineThirdPersonCameraData2
    {
        CinemachineVirtualCamera Camera { get; }
        Vector3 OffsetBottom { get; }
        Vector3 OffsetMid { get; }
        Vector3 OffsetTop { get; }
    }
}
