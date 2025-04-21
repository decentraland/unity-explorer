using Cinemachine;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineThirdPersonCameraData
    {
        CinemachineVirtualCamera Camera { get; }
        Vector3 OffsetBottom { get; }
        Vector3 OffsetMid { get; }
        Vector3 OffsetTop { get; }
        AnimationCurve DistanceScale { get; }

        public Cinemachine3rdPersonFollow ThirdPersonFollow { get; }
    }
}
