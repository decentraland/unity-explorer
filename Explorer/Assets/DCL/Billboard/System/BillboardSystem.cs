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
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.Unity.Billboard.System
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
            UpdateRotationQuery(World, exposedCameraData.WorldPosition);
        }

        [Query]
        private void UpdateRotation(
            [Data] in Vector3 cameraPosition,
            ref TransformComponent transform,
            in PBBillboard billboard
        )
        {
            var anglesLook = transform.Cached.WorldRotation.eulerAngles;
            var delta = cameraPosition - transform.Transform.position;
            var anglesTarget = Quaternion.LookRotation(delta, Vector3.up).eulerAngles;

            if (billboard.UseX())
                anglesLook.x = anglesTarget.x;

            if (billboard.UseY())
                anglesLook.y = anglesTarget.y;

            if (billboard.UseZ())
                anglesLook.z = anglesTarget.z;

            transform.Transform.rotation = Quaternion.Euler(anglesLook);
        }
    }
}
