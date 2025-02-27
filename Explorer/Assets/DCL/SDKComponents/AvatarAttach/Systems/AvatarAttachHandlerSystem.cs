using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Typing;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.SDKComponents.Utils;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR_ATTACH)]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription ENTITY_DESTRUCTION_QUERY = new QueryDescription().WithAll<DeleteEntityIntention, AvatarAttachComponent>();
        private static readonly QueryDescription COMPONENT_REMOVAL_QUERY = new QueryDescription().WithAll<AvatarAttachComponent>().WithNone<DeleteEntityIntention, PBAvatarAttach>();
        private readonly World globalWorld;

        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ISceneStateProvider sceneStateProvider;

        public AvatarAttachHandlerSystem(World world,
            World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;

            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured) return;

            UpdateAvatarAttachTransformQuery(World);
            HideDetachedQuery(World);

            World.Remove<AvatarAttachComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarAttachComponent, PBAvatarAttach>(ENTITY_DESTRUCTION_QUERY);
        }

        [Query]
        [All(typeof(AvatarAttachComponent))]
        [None(typeof(PBAvatarAttach))]
        private void HideDetached(ref TransformComponent transformComponent)
        {
            if (!sceneStateProvider.IsCurrent) return;
            transformComponent.Apply(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION);
        }

        [Query]
        private void UpdateAvatarAttachTransform(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbAvatarAttach.IsDirty)
            {
                AvatarBase? avatarBase = null;

                if (string.IsNullOrEmpty(pbAvatarAttach.AvatarId)) { avatarBase = mainPlayerAvatarBaseProxy.Object!; }
                else
                {
                    LightResult<AvatarBase> result = FindAvatarUtils.AvatarWithID(globalWorld, pbAvatarAttach.AvatarId);

                    if (result.Success)
                        avatarBase = result.Result;
                    else
                    {
                        transformComponent.Apply(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION);
                        return;
                    }
                }

                avatarAttachComponent = AvatarAttachUtils.GetAnchorPointTransform(pbAvatarAttach.AnchorPointId,
                    avatarBase
                );
            }

            if (AvatarAttachUtils.ApplyAnchorPointTransformValues(transformComponent, avatarAttachComponent))
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
    }
}
