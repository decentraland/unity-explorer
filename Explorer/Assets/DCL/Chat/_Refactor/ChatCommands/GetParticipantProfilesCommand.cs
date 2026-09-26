using DCL.Chat.History;
using DCL.Chat.ChatServices;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.Chat.ChatCommands
{
    public class GetParticipantProfilesCommand
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly CurrentChannelService currentChannelService;
        private readonly HashSet<string> processedIdentities = new (32);

        public GetParticipantProfilesCommand(IRoomHub roomHub, IProfileCache profileCache, CurrentChannelService currentChannelService)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
            this.currentChannelService = currentChannelService;
        }

        public void Execute(List<Profile.CompactInfo> targetList)
        {
            targetList.Clear();
            processedIdentities.Clear();

            // Add participants from local rooms (Island + Scene)
            foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
            {
                if (string.IsNullOrEmpty(identity) || !processedIdentities.Add(identity))
                    continue;

                if (profileCache.TryGetCompact(identity, out Profile.CompactInfo profile))
                    targetList.Add(profile);
            }

            // When in a DM, always include the other participant so @ mention works
            if (currentChannelService.CurrentChannel?.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                string dmPartnerId = currentChannelService.CurrentChannelId.Id;

                if (!string.IsNullOrEmpty(dmPartnerId) && processedIdentities.Add(dmPartnerId) &&
                    profileCache.TryGetCompact(dmPartnerId, out Profile.CompactInfo dmPartnerProfile))
                    targetList.Add(dmPartnerProfile);
            }
        }
    }
}
