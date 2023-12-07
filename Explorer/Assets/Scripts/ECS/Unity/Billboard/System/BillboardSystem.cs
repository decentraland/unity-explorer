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
        private readonly QueryDescription cameraQueryDescription;

        public BillboardSystem(World world) : this(
            world,
            new QueryDescription().WithAll<CameraComponent, TransformComponent>()
        ) { }

        public BillboardSystem(World world, QueryDescription cameraQueryDescription) : base(world)
        {
            this.cameraQueryDescription = cameraQueryDescription;
        }

        protected override void Update(float t)
        {
            InstantiateComponentsQuery(World);
            UpdateStateQuery(World);
            UpdateRotationQuery(World, CameraPosition());
        }

        [Query]
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
            var delta = transform.Transform.position - cameraPosition;
            delta.Scale(billboard.AsVector3());
            transform.Transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 CameraPosition()
        {
            var cameraEntity = World.GetSingleInstanceEntityOrNull(cameraQueryDescription);

            if (cameraEntity.IsNull())
                return Vector3.zero;

            var component = World.Get<TransformComponent>(cameraEntity);
            return component.Transform.position;
        }
    }
}
