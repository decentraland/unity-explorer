using System;
using Utility.Times;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityDeepLinkNotification : NotificationBase
    {
        public readonly string CommunityId;

        private readonly string communityName;
        private readonly string thumbnail;

        public CommunityDeepLinkNotification(string communityName, string thumbnail, string communityId)
        {
            Type = NotificationType.COMMUNITY_DEEP_LINK;
            this.communityName = communityName;
            this.thumbnail = thumbnail;
            CommunityId = communityId;
            Timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds().ToString();
        }

        public override string GetHeader() =>
            $"Welcome to {communityName}!";

        public override string GetTitle() =>
            "Check out their profile to get up to date and start chatting.";

        public override string GetThumbnail() =>
            thumbnail;
    }
}
