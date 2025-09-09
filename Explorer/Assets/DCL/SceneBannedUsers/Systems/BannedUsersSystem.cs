using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle.Realm;
using System.Threading;
using UnityEngine;
using Utility.Arch;

namespace DCL.SceneBannedUsers.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BannedUsersSystem : BaseUnityLoopSystem
    {
        private readonly IRealmNavigator realmNavigator;
        private readonly Vector2Int startParcelInGenesis;

        private BannedUsersSystem(
            World world,
            IRealmNavigator realmNavigator,
            Vector2Int startParcelInGenesis) : base(world)
        {
            this.realmNavigator = realmNavigator;
            this.startParcelInGenesis = startParcelInGenesis;
        }

        protected override void Update(float t) =>
            CheckBannedAvatarsQuery(World);

        [Query]
        private void CheckBannedAvatars(in Entity entity, ref AvatarShapeComponent avatarShapeComponent)
        {
            bool isBanned = BannedUsersFromCurrentScene.Instance.IsUserBanned(avatarShapeComponent.ID);

            if (!World.Has<PlayerComponent>(entity))
            {
                if (isBanned && !World.Has<BannedPlayerComponent>(entity))
                    World.Add(entity, new BannedPlayerComponent());
                else if (!isBanned)
                    World.TryRemove<BannedPlayerComponent>(entity);
            }
            else if (isBanned)
            {
                // If the banned user is the player, teleport him to Genesis Plaza
                realmNavigator.TeleportToParcelAsync(startParcelInGenesis, CancellationToken.None, false).Forget();
                BannedUsersFromCurrentScene.Instance.CleanBannedList();
            }
        }
    }
}
