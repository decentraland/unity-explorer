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
using DCL.FeatureFlags;
using DCL.Profiles;
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
                return new ChatTitlebarViewModel("Community not found");

            Sprite thumbnail = await getCommunityThumbnailCommand
                .ExecuteAsync(communityData.thumbnailUrl, ct);

            var viewModel = new ChatTitlebarViewModel(communityData.id, communityData.name, string.Empty)
            {
                ViewMode = TitlebarViewMode.Community,
            };

            viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(thumbnail, false, Color.gray, true));

            return viewModel;
        }

        private async UniTask<ChatTitlebarViewModel?> CreateUserViewModelAsync(ChatChannel channel, CancellationToken ct)
        {
            Profile.CompactInfo? compactInfo = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
            if (ct.IsCancellationRequested) return null; // TODO can't be null

            if (compactInfo == null)
            {
                var item = new ChatTitlebarViewModel(channel.Id.Id, $"{channel.Id.Id.Substring(0, 6)}...{channel.Id.Id.Substring(channel.Id.Id.Length - 4)}", channel.Id.Id);
                item.SetThumbnail(ProfileThumbnailViewModel.FromFallback(chatConfig.DefaultCommunityThumbnail));
                return item;

            }

            Profile.CompactInfo profile = compactInfo.Value;

            var userStatus = await getUserChatStatusCommand.ExecuteAsync(profile.UserId, ct);
            if (ct.IsCancellationRequested) return null;

            var isOfficial = OfficialWalletsHelper.Instance.IsOfficialWallet(profile.UserId);

            var viewModel = new ChatTitlebarViewModel(profile)
            {
                ViewMode = TitlebarViewMode.DirectMessage, IsOnline = userStatus.IsConsideredOnline, IsOfficial = isOfficial,
            };

            await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.Thumbnail, chatConfig.DefaultProfileThumbnail, profile, ct);

            return viewModel;
        }

        private ChatTitlebarViewModel CreateNearbyViewModel(ChatChannel channel)
        {
            var viewModel = new ChatTitlebarViewModel(chatConfig.NearbyConversationName)
            {
                ViewMode = TitlebarViewMode.Nearby,
            };

            viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(chatConfig.NearbyConversationIcon, true));
            return viewModel;
        }
    }
}
