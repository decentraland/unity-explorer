using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Billboard.Extensions;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;

namespace DCL.Billboard.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    public partial class BillboardSystem : BaseUnityLoopSystem
    {
        private const float MINIMUM_DISTANCE_TO_ROTATE_SQR = 0.25f * 0.25f;
        private readonly IExposedCameraData exposedCameraData;

        public BillboardSystem(World world, IExposedCameraData exposedCameraData) : base(world)
        {
            this.exposedCameraData = exposedCameraData;
        }

        protected override void Update(float t)
        {
            var cameraRotationAxisZ = Quaternion.Euler(0, 0, exposedCameraData.WorldRotation.Value.eulerAngles.z);
            Vector3 cameraPosition = exposedCameraData.WorldPosition;
            UpdateRotationQuery(World, cameraPosition, cameraRotationAxisZ);
        }

        [Query]
        private void UpdateRotation(
            [Data] in Vector3 cameraPosition,
            [Data] in Quaternion cameraRotationAxisZ,
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

            Transform billboardT = transform.Transform;
            Vector3 billboardForward = billboardT.forward;
            Vector3 billboardPos = billboardT.position;

            if ((cameraPosition - billboardPos).sqrMagnitude < MINIMUM_DISTANCE_TO_ROTATE_SQR)
                return;

            // either or both X and Y are set
            if ((billboardMode & BILLBOARD_XY) != 0)
            {
                billboardForward = billboardPos - cameraPosition;

                if ((billboardMode & BILLBOARD_Y) == 0) billboardForward.x = 0;
                if ((billboardMode & BILLBOARD_X) == 0) billboardForward.y = 0;

                billboardForward.Normalize();
            }

            Quaternion rotation = billboardForward != Vector3.zero ? Quaternion.LookRotation(billboardForward) : Quaternion.identity;

            // apply Z axis rotation
            if ((billboardMode & BILLBOARD_Z) != 0)
                rotation *= cameraRotationAxisZ;

            billboardT.rotation = rotation;
        }
    }
}
