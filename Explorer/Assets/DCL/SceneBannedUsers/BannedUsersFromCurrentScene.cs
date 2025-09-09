using CodeLess.Attributes;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;

namespace DCL.SceneBannedUsers
{
    [Singleton]
    public partial class BannedUsersFromCurrentScene
    {
        private readonly CurrentSceneBannedWalletsConfiguration bannedUsers;

        public BannedUsersFromCurrentScene(
            IScenesCache scenesCache,
            IRoomHub roomHub,
            CurrentSceneBannedWalletsConfiguration bannedUsers)
        {
            this.bannedUsers = bannedUsers;

            scenesCache.OnCurrentSceneChanged += OnCurrentSceneChanged;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;

        }

        public bool IsUserBanned(string userId)
        {
            foreach (string wallet in bannedUsers.bannedWallets)
            {
                if (string.Equals(wallet, userId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void CleanBannedList() =>
            bannedUsers.bannedWallets.Clear();

        private void OnCurrentSceneChanged(ISceneFacade scene)
        {

        }

        private void OnRoomMetadataChanged(string metaData)
        {

        }
    }
}
