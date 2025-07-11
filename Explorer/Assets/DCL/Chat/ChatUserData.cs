using System;
using UnityEngine;

namespace DCL.Chat
{
    /// <summary>
    /// A subset of a Profile, stores only the necessary data to be presented by the view.
    /// </summary>
    public struct ChatUserData
    {
        public string WalletAddress;
        public string Name;
        public Uri FaceSnapshotUrl;
        public string WalletId;
        public ChatMemberConnectionStatus ConnectionStatus;
        public Color ProfileColor;
    }
}
