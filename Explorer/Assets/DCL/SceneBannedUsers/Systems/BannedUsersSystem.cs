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
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BannedUsersSystem : BaseUnityLoopSystem
    {
        private BannedUsersSystem( World world) : base(world) { }

        protected override void Update(float t) =>
            CheckBannedAvatarsQuery(World);

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
