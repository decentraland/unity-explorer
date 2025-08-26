
namespace DCL.NotificationsBusController.NotificationTypes
{
    public class InvitationToCommunitySentNotification : NotificationBase
    {
        public override string GetHeader() =>
            "Invite to Community Sent";
    }
}
