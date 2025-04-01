using DCL.Web3;

namespace DCL.Friends.UI.BlockUserPrompt
{
    public struct BlockUserPromptParams
    {
        public readonly Web3Address TargetUserId;
        public readonly string TargetUserName;
        public readonly UserBlockAction Action;

        public BlockUserPromptParams(Web3Address targetUserId, string targetUserName, UserBlockAction action)
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
