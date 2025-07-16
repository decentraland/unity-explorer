using DCL.Web3;
using MVC;
using UnityEngine;

namespace DCL.Chat.Services
{
    /// <summary>
    /// Used to request showing the context menu for a specific user.
    /// Triggered by: Clicking on a user's name or profile picture in a chat message or member list.
    /// </summary>
    public struct UserProfileMenuRequest : IContextMenuRequest
    {
        public readonly Web3Address WalletAddress;
        public readonly Vector2 Position;
        public readonly Vector2 Offset;
        public readonly MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the options for a specific chat message (e.g., copy text).
    /// Triggered by: Clicking the "three dots" button on a chat entry.
    /// </summary>
    public struct ChatMessageMenuRequest : IContextMenuRequest
    {
        public readonly Vector2 Position;
        public readonly string MessageText;
        public readonly MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the options for the current channel (e.g., delete history).
    /// Triggered by: Clicking the "three dots" button in the chat's title bar.
    /// </summary>
    public struct ChannelMenuRequest : IContextMenuRequest
    {
        public readonly Vector2 Position;
        public readonly MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the "Paste" toast/popup.
    /// Triggered by: Right-clicking inside the chat input box.
    /// </summary>
    public struct PasteMenuRequest : IContextMenuRequest
    {
        public readonly Vector2 Position;
    }
}