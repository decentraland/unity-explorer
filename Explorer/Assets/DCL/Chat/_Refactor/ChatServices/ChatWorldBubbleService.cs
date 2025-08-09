using Arch.Core;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility.Arch;

namespace DCL.Chat.ChatServices
{
    public class ChatWorldBubbleService : IDisposable
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IProfileCache profileCache;
        private readonly NametagsData nametagsData;
        private readonly ChatSettingsAsset chatSettings;
        private readonly IChatHistory chatHistory;
        private readonly ICommunityDataService communityDataService;
        private static readonly Color DEFAULT_COLOR = Color.white;

        public ChatWorldBubbleService(
            World world,
            Entity playerEntity,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IProfileCache profileCache,
            NametagsData nametagsData,
            ChatSettingsAsset chatSettings,
            IChatHistory chatHistory,
            ICommunityDataService communityDataService)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.entityParticipantTable = entityParticipantTable;
            this.profileCache = profileCache;
            this.nametagsData = nametagsData;
            this.chatSettings = chatSettings;
            this.chatHistory = chatHistory;
            this.communityDataService = communityDataService;

            chatHistory.MessageAdded += OnChatMessageAdded;
            DCLInput.Instance.Shortcuts.ToggleNametags.performed += OnToggleNametagsShortcutPerformed;
        }

        private void OnChatMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };
            string? communityName = null;

            if (destinationChannel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
            {
                if (communityDataService.TryGetCommunity(destinationChannel.Id, out var communityData))
                {
                    communityName = communityData.name;
                }
            }

            CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser, communityName);
        }

        public void CreateChatBubble(ChatChannel channel, ChatMessage chatMessage, bool isSentByOwnUser, string? communityName = null)
        {
            if (!nametagsData.showNameTags || chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.NONE)
                return;

            if (chatMessage.IsSentByOwnUser == false && entityParticipantTable.TryGet(chatMessage.SenderWalletAddress, out var entry))
            {
                switch (channel.ChannelType)
                {
                    case ChatChannel.ChatChannelType.NEARBY:
                        GenerateChatBubbleComponent(entry.Entity, chatMessage, DEFAULT_COLOR, false, channel.Id);
                        break;
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        GenerateChatBubbleComponent(entry.Entity, chatMessage, DEFAULT_COLOR, false, channel.Id, null, null, true, communityName);
                        break;
                    case ChatChannel.ChatChannelType.USER:
                        GenerateChatBubbleComponent(entry.Entity, chatMessage, DEFAULT_COLOR, true, channel.Id);
                        break;
                }
            }
            else if (isSentByOwnUser)
            {
                switch (channel.ChannelType)
                {
                    case ChatChannel.ChatChannelType.NEARBY:
                        GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, false, channel.Id);
                        break;
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, false, channel.Id, null, null, true, communityName);
                        break;
                    case ChatChannel.ChatChannelType.USER:
                        // Chat bubbles appear if the channel is nearby or if settings allow them to appear for private conversations
                        if (chatSettings.chatBubblesVisibilitySettings == ChatBubbleVisibilitySettings.ALL)
                        {
                            if (!profileCache.TryGet(channel.Id.Id, out var profile))
                            {
                                GenerateChatBubbleComponent(playerEntity, chatMessage, DEFAULT_COLOR, true, channel.Id);
                            }
                            else
                            {
                                var nameColor = profile.UserNameColor != DEFAULT_COLOR ? profile.UserNameColor : ProfileNameColorHelper.GetNameColor(profile.DisplayName);
                                GenerateChatBubbleComponent(playerEntity, chatMessage, nameColor, true, channel.Id, profile.ValidatedName, profile.WalletId);
                            }
                        }

                        break;
                }
            }
        }

        private void GenerateChatBubbleComponent(Entity e, ChatMessage chatMessage, Color receiverNameColor, bool isPrivateMessage, ChatChannel.ChannelId messageChannelId, string? receiverDisplayName = null, string? receiverWalletId = null, bool isCommunityMessage = false, string? communityName = null)
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
                receiverNameColor,
                isCommunityMessage,
                communityName ?? string.Empty));
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.ToggleNametags.performed -= OnToggleNametagsShortcutPerformed;
            chatHistory.MessageAdded -= OnChatMessageAdded;
        }

        private void OnToggleNametagsShortcutPerformed(InputAction.CallbackContext obj)
        {
            nametagsData.showNameTags = !nametagsData.showNameTags;
        }
    }
}