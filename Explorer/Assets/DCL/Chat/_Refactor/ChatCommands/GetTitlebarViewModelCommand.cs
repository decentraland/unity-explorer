using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Communities;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System;
using System.Threading;
using DCL.Chat.ChatServices;
using UnityEngine;
using Utility;
using Color = UnityEngine.Color;

namespace DCL.Chat.ChatCommands
{
    public class GetTitlebarViewModelCommand
    {
        private readonly IEventBus eventBus;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ICommunityDataService communityDataService;
        private readonly GetCommunityThumbnailCommand getCommunityThumbnailCommand;
        private readonly GetUserChatStatusCommand getUserChatStatusCommand;
        private readonly ChatConfig.ChatConfig chatConfig;

        public GetTitlebarViewModelCommand(
            IEventBus eventBus,
            ICommunityDataService communityDataService,
            ProfileRepositoryWrapper profileRepository,
            ChatConfig.ChatConfig chatConfig,
            GetUserChatStatusCommand getUserChatStatusCommand,
            GetCommunityThumbnailCommand getCommunityThumbnailCommand)
        {
            this.eventBus = eventBus;
            this.communityDataService = communityDataService;
            this.profileRepository = profileRepository;
            this.getUserChatStatusCommand = getUserChatStatusCommand;
            this.getCommunityThumbnailCommand = getCommunityThumbnailCommand;
            this.chatConfig = chatConfig;
        }

        public async UniTask<ChatTitlebarViewModel?> ExecuteAsync(ChatChannel channel, CancellationToken ct)
        {
            return channel.ChannelType switch
            {
                ChatChannel.ChatChannelType.NEARBY => CreateNearbyViewModel(channel),
                ChatChannel.ChatChannelType.USER => await CreateUserViewModelAsync(channel, ct),
                ChatChannel.ChatChannelType.COMMUNITY => await CreateCommunityViewModelAsync(channel, ct),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private async UniTask<ChatTitlebarViewModel?> CreateCommunityViewModelAsync(ChatChannel channel, CancellationToken ct)
        {
            if (!communityDataService.TryGetCommunity(channel.Id, out var communityData))
            {
                return new ChatTitlebarViewModel
                {
                    Username = "Community not found"
                };
            }

            Sprite thumbnail = await getCommunityThumbnailCommand
                .ExecuteAsync(communityData.thumbnails?.raw, ct);

            var viewModel = new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.Community, Id = communityData.id, Username = communityData.name,
                ProfileColor = Color.gray
            };

            viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(thumbnail, false));

            return viewModel;
        }

        private async UniTask<ChatTitlebarViewModel?> CreateUserViewModelAsync(ChatChannel channel, CancellationToken ct)
        {
            var profile = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
            if (ct.IsCancellationRequested) return null; // TODO can't be null

            if (profile == null)
            {
                return new ChatTitlebarViewModel
                {
                    ViewMode = TitlebarViewMode.DirectMessage, Username = "User not found"
                };
            }

            var userStatus = await getUserChatStatusCommand.ExecuteAsync(profile.UserId, ct);
            if (ct.IsCancellationRequested) return null;
            
            var viewModel = new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.DirectMessage, Id = profile.UserId, Username = profile.Name, HasClaimedName = profile.HasClaimedName,
                WalletId = profile.WalletId!, ProfileColor = profile.UserNameColor, IsOnline = userStatus == ChatUserStateService.ChatUserState.CONNECTED
            };

            await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.Thumbnail, chatConfig.DefaultProfileThumbnail, profile.UserId, profile.Avatar.FaceSnapshotUrl, ct);

            return viewModel;
        }

        private ChatTitlebarViewModel CreateNearbyViewModel(ChatChannel channel)
        {
            var viewModel = new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.Nearby, Username = chatConfig.NearbyConversationName,
            };

            viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(chatConfig.NearbyConversationIcon, true));
            return viewModel;
        }
    }
}
