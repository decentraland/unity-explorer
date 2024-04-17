using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR_ATTACH)]
    [ThrottlingEnabled]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription ENTITY_DESTRUCTION_QUERY = new QueryDescription().WithAll<DeleteEntityIntention, AvatarAttachComponent>();
        private static readonly QueryDescription COMPONENT_REMOVAL_QUERY = new QueryDescription().WithAll<AvatarAttachComponent>().WithNone<DeleteEntityIntention, PBAvatarAttach>();
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ISceneStateProvider sceneStateProvider;

        public AvatarAttachHandlerSystem(World world, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured) return;

            SetupAvatarAttachQuery(World);
            UpdateAvatarAttachTransformQuery(World);

            World.Remove<AvatarAttachComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarAttachComponent, PBAvatarAttach>(ENTITY_DESTRUCTION_QUERY);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var component = new AvatarAttachComponent
            {
                anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId),
            };

            ApplyAnchorPointTransformValues(transformComponent, component);
            transformComponent.UpdateCache();

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttachTransform(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbAvatarAttach.IsDirty)
                avatarAttachComponent.anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId);

            if (ApplyAnchorPointTransformValues(transformComponent, avatarAttachComponent))
                transformComponent.UpdateCache();
        }

        [Query]
        [All(typeof(AvatarAttachComponent))]
        private void FinalizeComponents(in Entity entity)
        {
            World.Remove<AvatarAttachComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        private Transform GetAnchorPointTransform(AvatarAnchorPointType anchorPointType)
        {
            switch (anchorPointType)
            {
                case AvatarAnchorPointType.AaptLeftHand:
                    return mainPlayerAvatarBaseProxy.Object!.LeftHandAnchorPoint;
                case AvatarAnchorPointType.AaptRightHand:
                    return mainPlayerAvatarBaseProxy.Object!.RightHandAnchorPoint;
                case AvatarAnchorPointType.AaptNameTag:
                default: // AvatarAnchorPointType.AaptPosition
                    return mainPlayerAvatarBaseProxy.Object!.transform;
            }
        }

        private bool ApplyAnchorPointTransformValues(TransformComponent targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.anchorPointTransform.position;
            Quaternion anchorPointRotation = avatarAttachComponent.anchorPointTransform.rotation;
            var modifiedComponent = false;

            if (anchorPointPosition != targetTransform.Cached.WorldPosition)
            {
                targetTransform.Transform.position = anchorPointPosition;
                modifiedComponent = true;
            }

            if (anchorPointRotation != targetTransform.Cached.WorldRotation)
            {
                targetTransform.Transform.rotation = anchorPointRotation;
                modifiedComponent = true;
            }

            return modifiedComponent;
        }
    }
}
