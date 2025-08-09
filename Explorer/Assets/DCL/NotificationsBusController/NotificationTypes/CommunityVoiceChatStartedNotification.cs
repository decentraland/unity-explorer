namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityVoiceChatStartedNotification : NotificationBase
    {
        public readonly string CommunityName;
        public readonly string Thumbnail;

        public CommunityVoiceChatStartedNotification(string communityName, string thumbnail)
        {
            CommunityName = communityName;
            Thumbnail = thumbnail;
        }

        public override string GetHeader() =>
            "Community Voice Stream Started";

        public override string GetTitle() =>
            string.Format("Join from {0}'s chat to listen in.", CommunityName);

        public override string GetThumbnail() =>
            Thumbnail;
    }
}
