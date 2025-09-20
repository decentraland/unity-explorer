using System;
using Utility.Times;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityVoiceChatStartedNotification : NotificationBase
    {
        public readonly string CommunityName;
        public readonly string Thumbnail;
        public readonly string CommunityId;

        public CommunityVoiceChatStartedNotification(string communityName, string thumbnail, string communityId)
        {
            Type = NotificationType.COMMUNITY_VOICE_CHAT_STARTED;
            CommunityName = communityName;
            Thumbnail = thumbnail;
            CommunityId = communityId;
            Timestamp = DateTime.UtcNow.UnixTimeAsMilliseconds().ToString();
        }

        public override string GetHeader() =>
            "Community Voice Stream Started";

        public override string GetTitle() =>
            string.Format("The {0} is streaming! Click here to join the stream.", CommunityName);

        public override string GetThumbnail() =>
            Thumbnail;
    }
}
