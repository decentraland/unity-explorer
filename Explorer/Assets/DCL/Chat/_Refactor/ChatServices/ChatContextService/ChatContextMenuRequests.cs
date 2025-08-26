using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenuParameter;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.ChatServices.ChatContextService
{
    /// <summary>
    /// Used to request showing the context menu for a specific user.
    /// Triggered by: Clicking on a user's name or profile picture in a chat message or member list.
    /// </summary>
    public struct UserProfileMenuRequest
    {
        public Web3Address WalletAddress;
        public Vector2 Position;
        public Vector2 Offset;
        public MenuAnchorPoint AnchorPoint;

        public CancellationTokenSource CancellationTokenSource;
        public UniTaskCompletionSource CloseCompletionSource;
    }

    // A simple data packet to request a pre-configured context menu.
    public struct ShowContextMenuRequest
    {
        public GenericContextMenu MenuConfiguration;
        public Vector2 Position;
        public Vector2 Offset;
        public MenuAnchorPoint AnchorPoint;
    }


    public struct ChatContextMenuRequest
    {
        public Vector2 Position;
        public ChatOptionsContextMenuData contextMenuData;
        public Action OnDeleteHistory;
    }

    public struct ShowChannelContextMenuRequest
    {
        public GenericContextMenu MenuConfiguration;
        public Vector3 Position;
        public MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the options for a specific chat message (e.g., copy text).
    /// Triggered by: Clicking the "three dots" button on a chat entry.
    /// </summary>
    public struct ChatMessageMenuRequest
    {
        public readonly Vector2 Position;
        public readonly string MessageText;
        public readonly MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the options for the current channel (e.g., delete history).
    /// Triggered by: Clicking the "three dots" button in the chat's title bar.
    /// </summary>
    public struct ChannelMenuRequest
    {
        public readonly Vector2 Position;
        public readonly MenuAnchorPoint AnchorPoint;
    }
    
    /// <summary>
    /// Used to request showing the "Paste" toast/popup.
    /// Triggered by: Right-clicking inside the chat input box.
    /// </summary>
    public struct PasteMenuRequest
    {
        public readonly Vector2 Position;
    }
}