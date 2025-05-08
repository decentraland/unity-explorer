namespace DCL.NotificationsBusController.NotificationTypes
{
    public class MoveToParcelNotification : NotificationBase
    {
        public override string GetHeader() =>
            "Move to Parcel Received";

        public override string GetTitle() =>
            "You moved to a parcel, Welcome!";
    }
}
