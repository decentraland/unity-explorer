namespace DCL.Backpack.Gifting
{
    public static class GiftingTextIds
    {
        // Footer
        public const string DefaultInfoMessage =
            "Gifting an item cannot be undone.";

        public const string SelectedItemInfoMessageFormat =
            "You are about to send <b>{0}</b> to <b>{1}</b>";

        // Shared formatting
        public const string ColoredTextFormat =
            "<color=#{0}>{1}</color>";

        // Transfer in-progress
        public const string WaitingForWalletMessage =
            "A browser window should open for you to confirm the transaction.";

        public const string PreparingGiftTitle =
            "Preparing Gift for";

        public const string DefaultStatusMessage =
            "Processing...";
        
        public const string GiftSentTextFormat =
            "Gift Sent to <color=#{0}>{1}</color>!";

        public const string GiftReceivedTitleFormat =
            "<color=#{0}>{1}</color> sent you something!";

        public const string GiftReceivedFromFormat =
            "FROM <color=#{0}>{1}</color>";
        
        // Error dialog
        public const string ErrorDialogTitle =
            "Something went wrong";

        public const string ErrorDialogCancelText =
            "CLOSE";

        public const string ErrorDialogConfirmText =
            "TRY AGAIN";

        public const string ErrorDialogDescription =
            "Your gift wasn't delivered. Please try again or contact Support.";

        public const string ErrorDialogSupportLinkFormat =
            "<link=\"{0}\"><color=#D5A5E2>Contact Support</color></link>";

        public const string RetryLogMessage =
            "User clicked RETRY.";

        public const string JustNowMessage =
            "Just now.";
        
        public const string GiftOpenedTitle =
            "ITEM OPENED";

        public const string GiftLoading =
            "Loading...";
    }
}