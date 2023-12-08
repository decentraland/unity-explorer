using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Billboard.Component;
using ECS.Unity.Transforms.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.Unity.Billboard.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [ThrottlingEnabled]
    public partial class BillboardSystem : BaseUnityLoopSystem
    {
        private readonly IExposedCameraData exposedCameraData;

        public BillboardSystem(World world, IExposedCameraData exposedCameraData) : base(world)
        {
            this.exposedCameraData = exposedCameraData;
        }

        protected override void Update(float t)
        {
            InstantiateComponentsQuery(World);
            UpdateStateQuery(World);
            UpdateRotationQuery(World, CameraPosition());
        }

        [Query]
        [All(typeof(TransformComponent))]
        [None(typeof(BillboardComponent))]
        private void InstantiateComponents(in Entity entity, ref PBBillboard pbBillboard)
        {
            World.Add(entity, new BillboardComponent(pbBillboard));
        }

        [Query]
        private void UpdateState(ref PBBillboard pbBillboard, ref BillboardComponent billboard)
        {
            if (pbBillboard.IsDirty)
            {
                billboard.Apply(pbBillboard);
                pbBillboard.IsDirty = false;
            }
        }

        [Query]
        private void UpdateRotation(
            [Data] in Vector3 cameraPosition,
            ref TransformComponent transform,
            in BillboardComponent billboard
        )
        {
            var anglesLook = transform.Transform.rotation.eulerAngles;
            var delta = cameraPosition - transform.Transform.position;
            var anglesTarget = Quaternion.LookRotation(delta, Vector3.up).eulerAngles;

            if (billboard.UseX)
                anglesLook.x = anglesTarget.x;

            if (billboard.UseY)
                anglesLook.y = anglesTarget.y;

            if (billboard.UseZ)
                anglesLook.z = anglesTarget.z;

            transform.Transform.rotation = Quaternion.Euler(anglesLook);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 CameraPosition()
        {
            return exposedCameraData.WorldPosition;
        }
    }
}
