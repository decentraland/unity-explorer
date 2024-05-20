using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.Interaction.Raycast.Systems
{
    [UpdateInGroup(typeof(RaycastGroup))]
    [ThrottlingEnabled] // as we react on Scene Changes
    public partial class InitializeRaycastSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<PBRaycastResult> raycastComponentPool;

        internal InitializeRaycastSystem(World world,
            IComponentPool<PBRaycastResult> raycastComponentPool
        ) : base(world)
        {
            this.raycastComponentPool = raycastComponentPool;
        }

        protected override void Update(float t)
        {
            HandleChangedComponentQuery(World);
            HandleNewComponentQuery(World);
            HandleMissingRaycastResultQuery(World);
        }

        [Query]
        [All(typeof(TransformComponent), typeof(PBRaycast))] // Ray origins from Entity
        [None(typeof(RaycastComponent))]
        private void HandleNewComponent(in Entity entity)
        {
            var comp = new RaycastComponent();
            PBRaycastResult? raycastResult = raycastComponentPool.Get();
            World.Add(entity, comp, raycastResult);
        }

        [Query]
        [All(typeof(TransformComponent), typeof(PBRaycast))]
        [None(typeof(PBRaycastResult))]
        private void HandleMissingRaycastResult(in Entity entity)
        {
            //I Dont like this, but the SDK removes the PBRaycastResult and does not add it when it adds the PBRaycast + we dont remove the RaycastComponent
            PBRaycastResult? raycastResult = raycastComponentPool.Get();
            World.Add(entity, raycastResult);
        }

        [Query]
        [All(typeof(TransformComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void HandleChangedComponent(ref PBRaycast raycast, ref RaycastComponent raycastComponent)
        {
            if (raycast.IsDirty)
            {
                //If dirty, we set executed to false, continuous raycasts always will have Executed as false.
                raycastComponent.Executed = false;
                raycast.IsDirty = false;
            }
        }
    }
}
