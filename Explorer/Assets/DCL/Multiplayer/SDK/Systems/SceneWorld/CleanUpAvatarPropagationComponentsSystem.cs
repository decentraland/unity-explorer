using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Systems;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    /// <summary>
    ///     Components local to the client can't be cleaned-up via <see cref="ResetDirtyFlagSystem{T}" />
    ///     as their clean-up should not be throttled. Otherwise, components are reported every frame
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class CleanUpAvatarPropagationComponentsSystem : BaseUnityLoopSystem
    {
        internal CleanUpAvatarPropagationComponentsSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResetSDKProfileQuery(World);
            ResetSceneCRDTEntityQuery(World);
            ResetAvatarEmoteCommandComponentQuery(World);
        }

        [Query]
        private void ResetSDKProfile(ref SDKProfile sdkProfile)
        {
            sdkProfile.IsDirty = false;
        }

        [Query]
        private void ResetSceneCRDTEntity(ref PlayerSceneCRDTEntity crdtEntity)
        {
            crdtEntity.IsDirty = false;
        }

        [Query]
        private void ResetAvatarEmoteCommandComponent(ref AvatarEmoteCommandComponent avatarEmoteCommandComponent)
        {
            avatarEmoteCommandComponent.IsDirty = false;
        }
    }
}
