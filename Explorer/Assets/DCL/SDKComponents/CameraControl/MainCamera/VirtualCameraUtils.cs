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

        public static bool TryGetVirtualCameraComponent(in World world, Dictionary<CRDTEntity,Entity> entitiesMap, CRDTEntity targetCRDTEntity, out VirtualCameraComponent? returnComponent)
        {
            returnComponent = null;
            if (!entitiesMap.TryGetValue(targetCRDTEntity, out Entity virtualCameraEntity)
                || !world.TryGet(virtualCameraEntity, out VirtualCameraComponent virtualCameraComponent))
                return false;

            returnComponent = virtualCameraComponent;

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

        public static void ConfigureVirtualCameraTransition(in World world, Dictionary<CRDTEntity,Entity> entitiesMap, IExposedCameraData cameraData, CRDTEntity virtualCamCRDTEntity, float distanceBetweenCameras)
        {
            var pbVirtualCamera = world.Get<PBVirtualCamera>(entitiesMap[virtualCamCRDTEntity]);

            // Using custom blends array doesn't work because there's no direct way of getting the custom blend index,
            // and we would have to hardcode it...
            if (pbVirtualCamera.DefaultTransition.TransitionModeCase == CameraTransition.TransitionModeOneofCase.Time)
            {
                float timeValue = pbVirtualCamera.DefaultTransition.Time;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = timeValue;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = timeValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;
            }
            else
            {
                float speedValue = pbVirtualCamera.DefaultTransition.Speed;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = speedValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;

                // SPEED = 1 -> 1 meter per second
                float blendTime = distanceBetweenCameras / speedValue;
                if (blendTime == 0)
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                else
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = blendTime;
            }
        }

        public static void ConfigureCameraLookAt(in World world, Dictionary<CRDTEntity,Entity> entitiesMap, in VirtualCameraComponent virtualCameraComponent)
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
    }
}
