using Arch.Core;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.CameraControl.MainCamera
{
    public static class VirtualCameraUtils
    {
        private const float MINIMUM_LOOK_AT_DISTANCE_SQR = 0.25f * 0.25f;

        public static bool TryGetVirtualCameraComponents(
            in World world,
            IReadOnlyDictionary<CRDTEntity,Entity> entitiesMap,
            CRDTEntity targetCRDTEntity,
            out VirtualCameraComponent? returnComponent,
            out PBVirtualCamera? returnPBComponent)
        {
            returnComponent = null;
            returnPBComponent = null;

            if (!entitiesMap.TryGetValue(targetCRDTEntity, out Entity virtualCameraEntity)
                || !world.TryGet(virtualCameraEntity, out VirtualCameraComponent virtualCameraComponent)
                || !world.TryGet(virtualCameraEntity, out PBVirtualCamera? pbVirtualCameraComponent))
                return false;

            returnComponent = virtualCameraComponent;
            returnPBComponent = pbVirtualCameraComponent;

            return true;
        }

        public static CRDTEntity? GetPBVirtualCameraLookAtCRDTEntity(in PBVirtualCamera pbVirtualCamera, CRDTEntity virtualCameraCRDTEntity)
        {
            if (pbVirtualCamera.HasLookAtEntity)
            {
                int targetEntity = (int)pbVirtualCamera.LookAtEntity;
                if (targetEntity != SpecialEntitiesID.CAMERA_ENTITY && targetEntity != virtualCameraCRDTEntity.Id)
                    return new CRDTEntity(targetEntity);
            }

            return null;
        }

        public static void ConfigureVirtualCameraTransition(IExposedCameraData cameraData, in PBVirtualCamera pbVirtualCamera, float distanceBetweenCameras)
        {
            // Using cinemachine custom blends array doesn't work because there's no direct way of getting the custom
            //  blend index, and we would have to hardcode it...
            if (pbVirtualCamera.DefaultTransition == null)
            {
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                return;
            }

            switch (pbVirtualCamera.DefaultTransition.TransitionModeCase)
            {
                case CameraTransition.TransitionModeOneofCase.Time:
                    float timeValue = pbVirtualCamera.DefaultTransition.Time;
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = timeValue;
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = timeValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;
                    break;
                case CameraTransition.TransitionModeOneofCase.Speed:
                    float speedValue = pbVirtualCamera.DefaultTransition.Speed;
                    if (speedValue > 0)
                    {
                        cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.EaseInOut;
                        float blendTime = CalculateDistanceBlendTime(distanceBetweenCameras, speedValue); // SPEED = 1 -> 1 meter per second
                        if (blendTime == 0)
                            cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                        else
                            cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = blendTime;
                    }
                    else
                    {
                        cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                    }
                    break;
                default:
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                    break;
            }
        }

        public static void ConfigureCameraLookAt(in World world, IReadOnlyDictionary<CRDTEntity,Entity> entitiesMap, in VirtualCameraComponent virtualCameraComponent)
        {
            var rig = virtualCameraComponent.virtualCameraInstance.GetRig(1); // Middle (Aiming) Rig
            if (virtualCameraComponent.lookAtCRDTEntity.HasValue &&
                entitiesMap.TryGetValue(virtualCameraComponent.lookAtCRDTEntity.Value, out Entity lookAtEntity)
                && world.TryGet(lookAtEntity, out TransformComponent transformComponent)
                && (virtualCameraComponent.virtualCameraInstance.transform.position - transformComponent.Transform.position).sqrMagnitude >= MINIMUM_LOOK_AT_DISTANCE_SQR)
            {
                virtualCameraComponent.virtualCameraInstance.m_LookAt = transformComponent.Transform;
                rig.AddCinemachineComponent<CinemachineHardLookAt>();
            }
            else
            {
                rig.AddCinemachineComponent<CinemachinePOV>();
                virtualCameraComponent.virtualCameraInstance.m_LookAt = null;
            }
        }

        public static bool VirtualCameraExistsInEntitiesMap(IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, CRDTEntity? cameraCRDTEntity) =>
            cameraCRDTEntity.HasValue &&
            entitiesMap.TryGetValue(cameraCRDTEntity.Value, out Entity archEntity);

        public static void ConfigureVirtualCameraFOV(in PBVirtualCamera pbVirtualCamera, in VirtualCameraComponent virtualCameraComponent)
        {
            if (!pbVirtualCamera.HasFov) return;

            var freeLookCamera = virtualCameraComponent.virtualCameraInstance;

            // Apply to all rigs (top, middle, bottom)
            freeLookCamera.m_CommonLens = true;
            freeLookCamera.m_Lens.FieldOfView = pbVirtualCamera.Fov;
        }

        internal static float CalculateDistanceBlendTime(float distanceBetweenCameras, float speedValue) =>
            distanceBetweenCameras / speedValue;
    }
}
