using DCL.NotificationsBus.NotificationTypes;

namespace DCL.Backpack.Gifting
{
    public static class GiftingTextIds
    {
        // Footer
        public const string DEFAULT_INFO_MESSAGE =
            "Gifting an item cannot be undone.";

        public const string SELECTED_ITEM_INFO_MESSAGE_FORMAT =
            "You are about to send <b>{0}</b> to <b>{1}</b>";

        // Shared formatting
        public const string COLORED_TEXT_FORMAT =
            "<color=#{0}>{1}</color>";

        // Transfer in-progress
        public const string WAITING_FOR_WALLET_MESSAGE =
            "A browser window should open for you to confirm the transaction.";

        public const string WAITING_FOR_WALLET_MESSAGE_THIRD_WEB =
            "A window should open for you to confirm the transaction.";

        public const string PREPARING_GIFT_TITLE =
            "Preparing Gift for";

        public const string DEFAULT_STATUS_MESSAGE =
            "Processing...";
        
        public const string GIFT_SENT_TEXT_FORMAT =
            "Gift Sent to <color=#{0}>{1}</color>!";

        // Aliased from DCL.SharedAPI: this assembly depends on it, so the copy's single
        // definition cannot live here without creating a circular assembly reference.
        public const string GIFT_ON_ITS_WAY_MESSAGE =
            GiftReceivedNotification.GIFT_ON_ITS_WAY_MESSAGE;

        public const string GIFT_RECEIVED_TITLE_FORMAT =
            "<color=#{0}>{1}</color> sent you something! " + GIFT_ON_ITS_WAY_MESSAGE;

        public const string GIFT_RECEIVED_SENDER_TITLE_FORMAT =
            "{0} sent you something! " + GIFT_ON_ITS_WAY_MESSAGE;

        public const string GIFT_RECEIVED_FROM_FORMAT =
            "FROM <color=#{0}>{1}</color>";
        
        // Error dialog
        public const string ERROR_DIALOG_TITLE =
            "Something went wrong";

        public const string ERROR_DIALOG_CANCEL_TEXT =
            "CLOSE";

        public const string ERROR_DIALOG_CONFIRM_TEXT =
            "TRY AGAIN";

        public const string ERROR_DIALOG_DESCRIPTION =
            "Your gift wasn't delivered. Please try again or contact Support.";

        public const string ERROR_DIALOG_SUPPORT_LINK_FORMAT =
            "<link=\"{0}\"><color=#D5A5E2>Contact Support</color></link>";

        public const string RETRY_LOG_MESSAGE =
            "User clicked RETRY.";

        public const string JUST_NOW_MESSAGE =
            "Just now.";
        
        public const string GIFT_OPENED_TITLE =
            "ON ITS WAY TO YOUR BACKPACK";

        public const string GIFT_LOADING =
            "Loading...";
    }
}