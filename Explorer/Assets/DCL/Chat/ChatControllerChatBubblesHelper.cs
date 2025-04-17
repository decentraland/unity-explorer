using Arch.Core;
using UnityEngine;
using DCL.Nametags;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Settings.Settings;
using Utility.Arch;
using DCL.UI.Profiles.Helpers;

namespace DCL.Chat
{
    public class ChatControllerChatBubblesHelper
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IProfileCache profileCache;
        private readonly NametagsData nametagsData;
        private readonly ChatSettingsAsset chatSettings;
        private static readonly Color DEFAULT_COLOR = Color.white;

        public ChatControllerChatBubblesHelper(
            World world,
            Entity playerEntity,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IProfileCache profileCache,
            NametagsData nametagsData,
            ChatSettingsAsset chatSettings)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.entityParticipantTable = entityParticipantTable;
            this.profileCache = profileCache;
            this.nametagsData = nametagsData;
            this.chatSettings = chatSettings;
        }

        public void CreateChatBubble(ChatChannel channel, ChatMessage chatMessage, bool isSentByOwnUser)
        {
            if (!nametagsData.showNameTags || chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.NONE)
                return;

            if (chatMessage.IsSentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.SenderWalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                bool isPrivateMessage = channel.ChannelType == ChatChannel.ChatChannelType.USER;

                // Chat bubbles appear if the channel is nearby or if settings allow them to appear for private conversations
                if (!isPrivateMessage || chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.ALL)
                    GenerateChatBubbleComponent(entry.Entity, chatMessage, DEFAULT_COLOR, isPrivateMessage, channel.Id);
            }
            else if (isSentByOwnUser)
            {
                if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
                {
                    // Chat bubbles appear if the channel is nearby or if settings allow them to appear for private conversations
                    if (chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.ALL)
                    {
                        if (!profileCache.TryGet(channel.Id.Id, out var profile))
                        {
                            GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, true, channel.Id);
                        }
                        else
                        {
                            Color nameColor = profile.UserNameColor != DEFAULT_COLOR ? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(profile.DisplayName);
                            GenerateChatBubbleComponent(playerEntity, chatMessage, nameColor, true, channel.Id, profile.ValidatedName, profile.WalletId);
                        }
                    }
                }
                else
                    GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, false, channel.Id);
            }
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage, Color receiverNameColor, bool isPrivateMessage, ChatChannel.ChannelId messageChannelId, string? receiverDisplayName = null, string? receiverWalletId = null)
        {
            world.AddOrSet(e, new ChatBubbleComponent(
                chatMessage.Message,
                chatMessage.SenderValidatedName,
                chatMessage.SenderWalletAddress,
                chatMessage.IsMention,
                isPrivateMessage,
                messageChannelId.Id,
                chatMessage.IsSentByOwnUser,
                receiverDisplayName ?? string.Empty,
                receiverWalletId ?? string.Empty,
                receiverNameColor));
        }
    }
}
