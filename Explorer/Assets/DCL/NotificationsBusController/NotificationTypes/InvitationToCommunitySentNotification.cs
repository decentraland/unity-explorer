using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class InvitationToCommunitySentNotification : NotificationBase
    {
        public override string GetHeader() =>
            "Invitation to Community Sent";
    }
}
