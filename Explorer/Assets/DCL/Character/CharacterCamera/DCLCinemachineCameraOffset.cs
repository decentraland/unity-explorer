using Cinemachine;
using Cinemachine.Utility;
using UnityEngine;

namespace DCL.CharacterCamera
{
    [AddComponentMenu("")] // Hide in menu
    [ExecuteAlways]
    [SaveDuringPlay]
    public class DCLCinemachineCameraOffset : CinemachineExtension
    {
        [Tooltip("Offset the camera's position by this much (camera space)")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("When to apply the offset")]
        public CinemachineCore.Stage applyAfter = CinemachineCore.Stage.Aim;

        [Tooltip("If applying offset after aim, re-adjust the aim to preserve the screen position of the LookAt target as much as possible")]
        public bool preserveComposition;

        [Tooltip("Minimum distance to keep from obstacles")]
        public float minimumDistanceFromObstacle = 0.1f;

        public CinemachineCollider collider;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == applyAfter)
            {
                bool preserveAim = preserveComposition
                                   && state.HasLookAt && stage > CinemachineCore.Stage.Body;

                Vector3 screenOffset = Vector2.zero;

                if (preserveAim)
                {
                    screenOffset = state.RawOrientation.GetCameraRotationToTarget(
                        state.ReferenceLookAt - state.CorrectedPosition, state.ReferenceUp);
                }

                Vector3 desiredPosition = state.RawOrientation * this.offset;

                if (collider)
                {
                    float distance = desiredPosition.magnitude;

                    if (Physics.Raycast(new Ray(state.CorrectedPosition, desiredPosition.normalized),
                            out RaycastHit hit, distance, collider.m_CollideAgainst))
                        desiredPosition = desiredPosition.normalized * (hit.distance - minimumDistanceFromObstacle);
                }

                state.PositionCorrection += desiredPosition;

                if (!preserveAim)
                    state.ReferenceLookAt += desiredPosition;
                else
                {
                    var q = Quaternion.LookRotation(
                        state.ReferenceLookAt - state.CorrectedPosition, state.ReferenceUp);

                    q = q.ApplyCameraRotation(-screenOffset, state.ReferenceUp);
                    state.RawOrientation = q;
                }
            }
        }
    }
}
