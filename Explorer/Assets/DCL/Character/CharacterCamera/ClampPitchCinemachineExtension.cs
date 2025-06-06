using Cinemachine;
using UnityEngine;

namespace DCL.Character.CharacterCamera
{
    /// <summary>
    ///     An add-on module for Cinemachine Virtual Camera that locks camera's at horizon level when it goes lower
    /// </summary>
    [SaveDuringPlay] [AddComponentMenu("")]
    public class ClampPitchCinemachineExtension : CinemachineExtension
    {
        [SerializeField] private float m_YPosition;
        [SerializeField] private CinemachineCore.Stage Stage = CinemachineCore.Stage.Finalize;

        private float cachedY;
        private bool isCached;

        public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam, ref CameraState curState, float deltaTime)
        {
            float pitch = Mathf.DeltaAngle(0f, curState.RawOrientation.eulerAngles.x);

            if (pitch >= 0f)
            {
                cachedY = curState.RawPosition.y;
                isCached = true;
            }
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != Stage)
                return;

            float pitch = Mathf.DeltaAngle(0, state.RawOrientation.eulerAngles.x);

            if (isCached && pitch < 0)
            {
                Vector3 pos = state.RawPosition;
                pos.y = cachedY; //vcam.Follow.position.y;
                state.RawPosition = pos;
            }
        }
    }
}
