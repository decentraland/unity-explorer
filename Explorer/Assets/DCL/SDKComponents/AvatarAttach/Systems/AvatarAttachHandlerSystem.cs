using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Typing;
using DCL.Multiplayer.Profiles.Tables;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.SDKComponents.Utils;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;

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
        private readonly ObjectProxy<IReadOnlyEntityParticipantTable> entityParticipantTableProxy;

        public AvatarAttachHandlerSystem(
            World world,
            World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ISceneStateProvider sceneStateProvider,
            ObjectProxy<IReadOnlyEntityParticipantTable> entityParticipantTableProxy) : base(world)
        {
            this.globalWorld = globalWorld;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
            this.entityParticipantTableProxy = entityParticipantTableProxy;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured || !entityParticipantTableProxy.Configured) return;

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

                if (string.IsNullOrEmpty(pbAvatarAttach.AvatarId))
                {
                    avatarBase = mainPlayerAvatarBaseProxy.Object!;
                }
                else
                {
                    LightResult<AvatarBase> result = FindAvatarUtils.AvatarWithID(globalWorld, pbAvatarAttach.AvatarId, entityParticipantTableProxy.Object);

                    if (result.Success)
                        avatarBase = result.Result;
                    else
                    {
                        ReportHub.Log(ReportCategory.AVATAR_ATTACH, $"Failed to find avatar with ID {pbAvatarAttach.AvatarId}");
                        transformComponent.Apply(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION);
                        return;
                    }
                }

                try
                {
                    avatarAttachComponent = AvatarAttachUtils.GetAnchorPointTransform(pbAvatarAttach.AnchorPointId,
                        avatarBase
                    );
                }
                catch (Exception ex)
                {
                    ReportHub.Log(ReportCategory.AVATAR_ATTACH, $"Error getting anchor point transform: {ex.Message}");
                    transformComponent.Apply(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION);
                    return;
                }
            }

            try
            {
                if (AvatarAttachUtils.ApplyAnchorPointTransformValues(transformComponent, avatarAttachComponent))
                    transformComponent.UpdateCache();
            }
            catch (Exception ex)
            {
                ReportHub.Log(ReportCategory.AVATAR_ATTACH, $"Error applying anchor point transform values: {ex.Message}");
                transformComponent.Apply(MordorConstants.AVATAR_ATTACH_MORDOR_POSITION);
            }
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
