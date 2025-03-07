namespace DCL.Friends.UI.BlockUserPrompt
{
    public struct BlockUserPromptParams
    {
        public readonly string TargetUserId;
        public readonly string TargetUserName;
        public readonly UserBlockAction Action;

        public BlockUserPromptParams(string targetUserId, string targetUserName, UserBlockAction action)
        {
            this.TargetUserId = targetUserId;
            this.TargetUserName = targetUserName;
            this.Action = action;
        }

        public enum UserBlockAction
        {
            BLOCK,
            UNBLOCK
        }
    }
}
