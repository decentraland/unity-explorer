namespace DCL.AvatarRendering.Emotes
{
    public struct TriggerEmoteBySlotIntent
    {
        public int Slot { get; set; }

        /// <summary>
        /// When sending a directed emote, the wallet address of the player that will receive the invitation is stored here.
        /// </summary>
        public string TargetAvatarWalletAddress;
    }
}
