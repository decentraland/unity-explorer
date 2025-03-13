using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.Components;
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
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    // Note: This system was extracted from AvatarAttachHandlerSystem in order to make Throttling possible
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(AvatarAttachHandlerSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.AVATAR_ATTACH)]
    public partial class AvatarAttachHandlerSetupSystem : BaseUnityLoopSystem
    {
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly World globalWorld;
        private readonly ObjectProxy<IReadOnlyEntityParticipantTable> entityParticipantTableProxy;

        public AvatarAttachHandlerSetupSystem(
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

            SetupAvatarAttachQuery(World);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            if (!sceneStateProvider.IsCurrent) return;

            AvatarBase avatarBase;

            if (string.IsNullOrEmpty(pbAvatarAttach.AvatarId))
            {
                avatarBase = mainPlayerAvatarBaseProxy.Object!;
            }
            else
            {
                LightResult<AvatarBase> result = FindAvatarUtils.AvatarWithID(globalWorld, pbAvatarAttach.AvatarId, entityParticipantTableProxy.Object);
                if (!result.Success)
                {
                    ReportHub.Log(ReportCategory.AVATAR_ATTACH, $"Failed to find avatar with ID {pbAvatarAttach.AvatarId} for entity {entity}");
                    return;
                }
                avatarBase = result.Result;
            }

            AvatarAttachComponent component = AvatarAttachUtils.GetAnchorPointTransform(pbAvatarAttach.AnchorPointId, avatarBase);

            AvatarAttachUtils.ApplyAnchorPointTransformValues(transformComponent, component);
            transformComponent.UpdateCache();

            World.Add(entity, component);
        }
    }
}
