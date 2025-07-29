using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.Chat.ChatUseCases
{
    public class GetParticipantProfilesCommand
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;

        public GetParticipantProfilesCommand(IRoomHub roomHub, IProfileCache profileCache)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
        }

        public void Execute(List<Profile> targetList)
        {
            targetList.Clear();

            foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
            {
                // TODO: Use new endpoint to get a bunch of profile info
                if (profileCache.TryGet(identity, out Profile profile))
                    targetList.Add(profile);
            }
        }
    }
}
