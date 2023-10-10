﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.Interaction.Raycast.Systems
{
    [UpdateInGroup(typeof(RaycastGroup))]
    [ThrottlingEnabled] // as we react on Scene Changes
    public partial class InitializeRaycastSystem : BaseUnityLoopSystem
    {
        internal InitializeRaycastSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            HandleChangedComponentQuery(World);
            HandleNewComponentQuery(World);
        }

        [Query]
        [All(typeof(TransformComponent), typeof(PBRaycast))] // Ray origins from Entity
        [None(typeof(RaycastComponent))]
        private void HandleNewComponent(in Entity entity)
        {
            var comp = new RaycastComponent();
            World.Add(entity, comp);
        }

        [Query]
        [All(typeof(TransformComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void HandleChangedComponent(ref PBRaycast raycast, ref RaycastComponent raycastComponent)
        {
            if (raycast.IsDirty)
            {
                if (raycast.Continuous) raycastComponent.Executed = false;
                raycast.IsDirty = false;
            }
        }
    }
}
