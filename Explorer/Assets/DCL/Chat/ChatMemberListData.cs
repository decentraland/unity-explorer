using UnityEngine;

namespace DCL.Chat
{
    /// <summary>
    ///     A subset of a Profile, stores only the necessary data to be presented by the view.
    /// </summary>
    public struct ChatMemberListData
    {
        public string Id;
        public string Name;
        public string FaceSnapshotUrl;
        public string WalletId;
        public ChatMemberConnectionStatus ConnectionStatus;
        public Color ProfileColor;
        public bool HasClaimedName;
    }
}
