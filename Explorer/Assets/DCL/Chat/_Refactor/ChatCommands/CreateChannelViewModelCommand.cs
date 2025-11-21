using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class CreateChannelViewModelCommand
    {
        private readonly IEventBus eventBus;
        private readonly ICommunityDataService communityDataService;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly GetCommunityThumbnailCommand getCommunityThumbnailCommand;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly GetUserChatStatusCommand getUserChatStatusCommand;

        public CreateChannelViewModelCommand(
            IEventBus eventBus,
            ICommunityDataService communityDataService,
            ChatConfig.ChatConfig chatConfig,
            ProfileRepositoryWrapper profileRepository,
            GetUserChatStatusCommand getUserChatStatusCommand,
            GetCommunityThumbnailCommand getCommunityThumbnailCommand,
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.eventBus = eventBus;
            this.communityDataService = communityDataService;
            this.chatConfig = chatConfig;
            this.profileRepository = profileRepository;
            this.getUserChatStatusCommand = getUserChatStatusCommand;
            this.getCommunityThumbnailCommand = getCommunityThumbnailCommand;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
        }

        public BaseChannelViewModel CreateViewModelAndFetch(ChatChannel channel, CancellationToken ct)
        {
            int unreadCount = channel.Messages.Count - channel.ReadMessages;
            var hasMentions = false;
            if (unreadCount > 0)
            {
                for (int i = channel.ReadMessages; i < channel.Messages.Count; i++)
                {
                    if (channel.Messages[i].IsMention)
                    {
                        hasMentions = true;
                        break;
                    }
                }
            }

            BaseChannelViewModel viewModel = channel.ChannelType switch
            {
                ChatChannel.ChatChannelType.NEARBY =>
                    new NearbyChannelViewModel(channel.Id,
                        chatConfig.NearbyConversationName,
                        chatConfig.NearbyConversationIcon,
                        unreadCount,
                        hasMentions),

                ChatChannel.ChatChannelType.USER =>
                    CreateUserChannelViewModel(channel, unreadCount, hasMentions, ct),

                ChatChannel.ChatChannelType.COMMUNITY =>
                    CreateCommunityChannelViewModel(channel, unreadCount, hasMentions, ct),

                _ => throw new ArgumentOutOfRangeException(nameof(channel.ChannelType), "Unsupported channel type")
            };

            viewModel.UnreadMessagesCount = channel.Messages.Count - channel.ReadMessages;
            return viewModel;
        }

        private UserChannelViewModel CreateUserChannelViewModel(ChatChannel channel, int unreadCount, bool hasMentions, CancellationToken ct)
        {
            var viewModel = new UserChannelViewModel(channel.Id, unreadCount, hasMentions);
            FetchProfileAndUpdateAsync(viewModel, ct).Forget();
            FetchInitialStatusAndUpdateAsync(viewModel, ct).Forget();
            return viewModel;
        }

        private CommunityChannelViewModel CreateCommunityChannelViewModel(ChatChannel channel, int unreadCount, bool hasMentions, CancellationToken ct)
        {
            var viewModel = new CommunityChannelViewModel(channel.Id, unreadCount, hasMentions);

            if (communityDataService.TryGetCommunity(channel.Id, out GetUserCommunitiesData.CommunityData communityData))
            {
                viewModel.DisplayName = communityData.name;
                viewModel.ImageUrl = communityData.thumbnailUrl;
                viewModel.CommunityConnectionUpdates = voiceChatOrchestrator.CommunityConnectionUpdates(ChatChannel.GetCommunityIdFromChannelId(channel.Id));
                viewModel.CurrentCommunityCallId = voiceChatOrchestrator.CurrentCommunityId;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Created ViewModel for: {communityData.name} -> is Streaming: {viewModel.CommunityConnectionUpdates.Value} - current community ID: {viewModel.CurrentCommunityCallId.Value}");

                FetchCommunityThumbnailAndUpdateAsync(viewModel, ct).Forget();
            }

            return viewModel;
        }

        private async UniTaskVoid FetchCommunityThumbnailAndUpdateAsync(CommunityChannelViewModel viewModel, CancellationToken ct)
        {
            Sprite? thumbnail = await getCommunityThumbnailCommand
               .ExecuteAsync(viewModel.ImageUrl, ct);

            if (ct.IsCancellationRequested) return;

            viewModel.Thumbnail = thumbnail;

            eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
            {
                ViewModel = viewModel,
            });
        }

        private async UniTaskVoid FetchProfileAndUpdateAsync(UserChannelViewModel viewModel, CancellationToken ct)
        {
            Profile.CompactInfo? profile = await profileRepository.GetProfileAsync(viewModel.Id.Id, ct).SuppressAnyExceptionWithFallback(null);

            if (ct.IsCancellationRequested) return;

            string userId = viewModel.Id.Id;

            if (profile != null)
            {
                viewModel.DisplayName = profile.Value.ValidatedName;
                viewModel.HasClaimedName = profile.Value.HasClaimedName;
                viewModel.IsOfficial = OfficialWalletsHelper.Instance.IsOfficialWallet(userId);

                viewModel.ProfilePicture.UpdateValue(viewModel.ProfilePicture.Value.SetColor(profile.Value.UserNameColor));

                await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfilePicture, chatConfig.DefaultProfileThumbnail, profile.Value.UserId, profile.Value.FaceSnapshotUrl, ct);
            }
            else
            {
                viewModel.DisplayName = $"{userId.Substring(0, 6)}...{userId.Substring(userId.Length - 4)}";
                viewModel.HasClaimedName = false;

                viewModel.ProfilePicture
                    .UpdateValue(new ProfileThumbnailViewModel.WithColor(ProfileThumbnailViewModel.FromLoaded(chatConfig.DefaultProfileThumbnail, true),
                        ProfileThumbnailViewModel.WithColor.DEFAULT_PROFILE_COLOR));
            }

            eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
            {
                ViewModel = viewModel,
            });
        }

        private async UniTaskVoid FetchInitialStatusAndUpdateAsync(UserChannelViewModel viewModel, CancellationToken ct)
        {
            var status = await getUserChatStatusCommand.ExecuteAsync(viewModel.Id.Id, ct);

            if (ct.IsCancellationRequested) return;

            viewModel.IsOnline = status.IsConsideredOnline;

            eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
            {
                ViewModel = viewModel,
            });
        }

        public async UniTask UpdateCommunityThumbnailAsync(CommunityChannelViewModel vm, CancellationToken ct)
        {
            var sprite = await getCommunityThumbnailCommand.ExecuteAsync(vm.ImageUrl, ct);
            if (ct.IsCancellationRequested) return;
            vm.Thumbnail = sprite;
            eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
            {
                ViewModel = vm
            });
        }
    }
}
