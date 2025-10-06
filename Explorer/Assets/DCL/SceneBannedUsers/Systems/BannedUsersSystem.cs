using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using ECS.Abstract;
using Utility.Arch;

namespace DCL.SceneBannedUsers.Systems
{
    /// <summary>
    /// System that checks if the players around are banned from the current scene and, if so, hides them.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BannedUsersSystem : BaseUnityLoopSystem
    {
        private BannedUsersSystem( World world) : base(world) { }

        protected override void Update(float t) =>
            CheckBannedAvatarsQuery(World);

        /// <summary>
        /// Adds/Removes the 'BannedPlayerComponent' component to/from the entity if any avatar around is banned or not. The 'BannedPlayerComponent' component will be
        /// checked by other systems like AvatarShapeVisibilitySystem, CharacterEmoteSystem, ProcessOtherAvatarsInteractionSystem, RemotePlayerAnimationSystem and NametagPlacementSystem
        /// to render or not the avatar and avoid any interaction with it.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="avatarShapeComponent"></param>
        [Query]
        [None(typeof(PlayerComponent))]
        private void CheckBannedAvatars(in Entity entity, ref AvatarShapeComponent avatarShapeComponent)
        {
            bool isBanned = BannedUsersFromCurrentScene.Instance.IsUserBanned(avatarShapeComponent.ID);

            if (isBanned && !World.Has<BannedPlayerComponent>(entity))
                World.Add(entity, new BannedPlayerComponent());
            else if (!isBanned)
                World.TryRemove<BannedPlayerComponent>(entity);
        }
    }
}
