using Cinemachine;
using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineThirdPersonCameraData : ICinemachineThirdPersonCameraData
    {
        [field: SerializeField] public CinemachineVirtualCamera Camera { get; private set; }

        [field: SerializeField] public Vector3 OffsetBottom { get; private set; }
        [field: SerializeField] public Vector3 OffsetMid { get; private set; }
        [field: SerializeField] public Vector3 OffsetTop { get; private set; }

        [field: SerializeField] public AnimationCurve DistanceScale { get; private set; }

        private Cinemachine3rdPersonFollow cachedFollow;
        public Cinemachine3rdPersonFollow ThirdPersonFollow => cachedFollow ??= Camera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
    }
}
