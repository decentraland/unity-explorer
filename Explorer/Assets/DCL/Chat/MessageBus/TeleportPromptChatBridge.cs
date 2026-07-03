using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.TeleportPrompt;
using System;
using UnityEngine;

namespace DCL.Chat.MessageBus
{
    /// <summary>
    ///     Subscribes to <see cref="TeleportPromptBus" /> and translates an approved teleport into
    ///     the "/goto" chat command, keeping the teleport prompt decoupled from the chat/social assemblies.
    /// </summary>
    public class TeleportPromptChatBridge : IDisposable
    {
        private readonly TeleportPromptBus teleportPromptBus;
        private readonly IChatMessagesBus chatMessagesBus;

        public TeleportPromptChatBridge(TeleportPromptBus teleportPromptBus, IChatMessagesBus chatMessagesBus)
        {
            this.teleportPromptBus = teleportPromptBus;
            this.chatMessagesBus = chatMessagesBus;

            teleportPromptBus.TeleportApproved += OnTeleportApproved;
        }

        public void Dispose() =>
            teleportPromptBus.TeleportApproved -= OnTeleportApproved;

        private void OnTeleportApproved(Vector2Int coords) =>
            chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {coords.x},{coords.y}", ChatMessageOrigin.TELEPORT_PROMPT);
    }
}
