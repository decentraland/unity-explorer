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
            if (billboard.BillboardMode is BillboardMode.BmNone)
                return;

            Vector3 forward = transform.Transform.forward;

            if (billboard.UseX() || billboard.UseY())
            {
                forward = cameraPosition - transform.Cached.WorldPosition;

                if ((billboard.BillboardMode & BillboardMode.BmY) == 0) forward.x = 0;
                if ((billboard.BillboardMode & BillboardMode.BmX) == 0) forward.y = 0;

                forward.Normalize();
            }

            Quaternion rotation = forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity;

            if (billboard.UseZ())
                rotation *= cameraRotationAxisZ;

            transform.Apply(rotation);
        }
    }
}
