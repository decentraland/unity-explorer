using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatUseCases.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using Utility;
using Color = UnityEngine.Color;

namespace DCL.Chat.ChatUseCases
{
    public class GetTitlebarViewModelCommand
    {
        private readonly IEventBus eventBus;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ICommunityDataService communityDataService;
        private readonly GetCommunityThumbnailCommand getCommunityThumbnailCommand;
        private readonly ChatConfig chatConfig;

        public GetTitlebarViewModelCommand(
            IEventBus eventBus,
            ICommunityDataService communityDataService,
            ProfileRepositoryWrapper profileRepository,
            ChatConfig chatConfig,
            GetCommunityThumbnailCommand getCommunityThumbnailCommand)
        {
            this.eventBus = eventBus;
            this.communityDataService = communityDataService;
            this.profileRepository = profileRepository;
            this.getCommunityThumbnailCommand = getCommunityThumbnailCommand;
            this.chatConfig = chatConfig;
        }

        public async UniTask<ChatTitlebarViewModel> ExecuteAsync(ChatChannel channel, CancellationToken ct)
        {
            return channel.ChannelType switch
            {
                ChatChannel.ChatChannelType.NEARBY => CreateNearbyViewModel(channel),
                ChatChannel.ChatChannelType.USER => await CreateUserViewModelAsync(channel, ct),
                ChatChannel.ChatChannelType.COMMUNITY => await CreateCommunityViewModelAsync(channel, ct),
                _ => throw new ArgumentOutOfRangeException()
            };

            var viewModel = new ChatTitlebarViewModel();

            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                viewModel.ViewMode = TitlebarViewMode.DirectMessage;
                viewModel.WalletId = new Web3Address(channel.Id.Id);

                Profile? profile = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
                if (ct.IsCancellationRequested || profile == null)
                {
                    return new ChatTitlebarViewModel
                    {
                        Username = "Unknown User"
                    };
                }

                viewModel.Id = profile.UserId;
                viewModel.Username = profile.Name;
                viewModel.HasClaimedName = profile.HasClaimedName;
                viewModel.WalletId = profile.WalletId;
                viewModel.ProfileColor = profile.UserNameColor;

                await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.Thumbnail, chatConfig.DefaultProfileThumbnail, profile.UserId, profile.Avatar.FaceSnapshotUrl, ct);
            }
            else
            {
                viewModel.ViewMode = TitlebarViewMode.Nearby;
                viewModel.Username = chatConfig.NearbyConversationName;
                viewModel.HasClaimedName = false;
                viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(chatConfig.NearbyConversationIcon, true));
            }

            return viewModel;
        }

        private async Task<ChatTitlebarViewModel> CreateCommunityViewModelAsync(ChatChannel channel, CancellationToken ct)
        {
            if (!communityDataService.TryGetCommunity(channel.Id, out var communityData))
            {
                return new ChatTitlebarViewModel
                {
                    Username = "Community not found"
                };
            }

            var thumbnail = await getCommunityThumbnailCommand
                .ExecuteAsync(communityData.thumbnails?.raw, ct);

            return new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.Community, Id = communityData.id, Username = communityData.name, ProfileSprite = thumbnail,
                ProfileColor = Color.gray
            };
        }

        private async Task<ChatTitlebarViewModel> CreateUserViewModelAsync(ChatChannel channel, CancellationToken ct)
        {
            var profile = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
            if (ct.IsCancellationRequested) return null;

            if (profile == null)
            {
                return new ChatTitlebarViewModel
                {
                    ViewMode = TitlebarViewMode.DirectMessage, Username = "User not found"
                };
            }

            var thumbnail = await getProfileThumbnailCommand
                .ExecuteAsync(profile.UserId, profile.Avatar.FaceSnapshotUrl, ct);

            return new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.DirectMessage, Id = profile.UserId, Username = profile.Name, HasClaimedName = profile.HasClaimedName,
                WalletId = profile.WalletId, ProfileColor = profile.UserNameColor, ProfileSprite = thumbnail
            };
        }

        private ChatTitlebarViewModel CreateNearbyViewModel(ChatChannel channel)
        {
            return new ChatTitlebarViewModel
            {
                ViewMode = TitlebarViewMode.Nearby, Username = chatConfig.NearbyConversationName, ProfileSprite = chatConfig.NearbyConversationIcon
            };
        }
    }
}
