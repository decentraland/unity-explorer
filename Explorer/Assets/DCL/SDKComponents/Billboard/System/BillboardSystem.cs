using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Billboard.Extensions;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Billboard.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    public partial class BillboardSystem : BaseUnityLoopSystem
    {
        private const float MINIMUM_DISTANCE_TO_ROTATE_SQR = 0.25f * 0.25f;
        private readonly IExposedCameraData exposedCameraData;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public BillboardSystem(World world, IExposedCameraData exposedCameraData, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap) : base(world)
        {
            this.exposedCameraData = exposedCameraData;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            // Get the active camera position and rotation from CinemachineBrain if available,
            // otherwise fall back to exposedCameraData values (player camera)
            Vector3 cameraPosition;
            Quaternion cameraRotation;

            var activeVirtualCamera = exposedCameraData.CinemachineBrain?.ActiveVirtualCamera;
            if (activeVirtualCamera != null)
            {
                var cameraTransform = activeVirtualCamera.VirtualCameraGameObject.transform;
                cameraPosition = cameraTransform.position;
                cameraRotation = cameraTransform.rotation;
            }
            else
            {
                cameraPosition = exposedCameraData.WorldPosition;
                cameraRotation = exposedCameraData.WorldRotation.Value;
            }

            var cameraRotationAxisZ = Quaternion.Euler(0, 0, cameraRotation.eulerAngles.z);
            UpdateRotationQuery(World, cameraPosition, cameraRotationAxisZ);
        }

        [Query]
        private void UpdateRotation(
            [Data] in Vector3 cameraPosition,
            [Data] in Quaternion cameraRotationAxisZ,
            Entity entity,
            ref TransformComponent transform,
            in PBBillboard billboard
        )
        {
            const uint BILLBOARD_NONE = (uint)BillboardMode.BmNone;
            const uint BILLBOARD_X = (uint)BillboardMode.BmX;
            const uint BILLBOARD_Y = (uint)BillboardMode.BmY;
            const uint BILLBOARD_Z = (uint)BillboardMode.BmZ;
            const uint BILLBOARD_XY = BILLBOARD_X | BILLBOARD_Y;

            var billboardMode = (uint)billboard.GetBillboardMode();

            if (billboardMode == BILLBOARD_NONE)
                return;

            Vector3 sourcePosition = cameraPosition;
            Quaternion sourceRotationAxisZ = cameraRotationAxisZ;

            if (billboard.HasTargetEntity && billboard.TargetEntity != SpecialEntitiesID.CAMERA_ENTITY)
            {
                if(TryGetTargetTransform(entity, billboard.TargetEntity, out TransformComponent targetTransform))
                {
                    Transform t = targetTransform.Transform;
                    sourcePosition = t.position;
                    sourceRotationAxisZ = Quaternion.Euler(0f, 0f, t.rotation.eulerAngles.z);
                }
                else
                    return; // target set but unresolved or self → billboard disabled this frame
            }

            Transform billboardT = transform.Transform;
            Vector3 billboardForward = billboardT.forward;
            Vector3 billboardPos = billboardT.position;

            if ((sourcePosition - billboardPos).sqrMagnitude < MINIMUM_DISTANCE_TO_ROTATE_SQR)
                return;

            // either or both X and Y are set
            if ((billboardMode & BILLBOARD_XY) != 0)
            {
                billboardForward = billboardPos - sourcePosition;

                if ((billboardMode & BILLBOARD_Y) == 0) billboardForward.x = 0;
                if ((billboardMode & BILLBOARD_X) == 0) billboardForward.y = 0;

                billboardForward.Normalize();
            }

            Quaternion rotation = billboardForward != Vector3.zero ? Quaternion.LookRotation(billboardForward) : Quaternion.identity;

            // apply Z axis rotation
            if ((billboardMode & BILLBOARD_Z) != 0)
                rotation *= sourceRotationAxisZ;

            billboardT.rotation = rotation;
        }

        private bool TryGetTargetTransform(Entity selfEntity, uint targetCrdtId, out TransformComponent targetTransform)
        {
            targetTransform = default(TransformComponent);
            return entitiesMap.TryGetValue(new CRDTEntity((int)targetCrdtId), out Entity targetEntity)
              && targetEntity != selfEntity
              && World.TryGet(targetEntity, out targetTransform);
        }
    }
}
