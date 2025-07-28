using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatUseCases.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.ChatViewModels.ChannelViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;

using Utility;

public class CreateChannelViewModelCommand
{
    private readonly IEventBus eventBus;
    private readonly ICommunityDataService communityDataService;
    private readonly ChatConfig chatConfig;
    private readonly ProfileRepositoryWrapper profileRepository;
    private readonly GetCommunityThumbnailCommand getCommunityThumbnailCommand;

    public CreateChannelViewModelCommand(
        IEventBus eventBus,
        ICommunityDataService communityDataService,
        ChatConfig chatConfig,
        ProfileRepositoryWrapper profileRepository,
        GetCommunityThumbnailCommand getCommunityThumbnailCommand)
    {
        this.eventBus = eventBus;
        this.communityDataService = communityDataService;
        this.chatConfig = chatConfig;
        this.profileRepository = profileRepository;
        this.getCommunityThumbnailCommand = getCommunityThumbnailCommand;
    }

    public BaseChannelViewModel CreateViewModelAndFetch(ChatChannel channel, CancellationToken ct)
    {
        BaseChannelViewModel viewModel = channel.ChannelType switch
        {
            ChatChannel.ChatChannelType.NEARBY =>
                new NearbyChannelViewModel(channel.Id, chatConfig.NearbyConversationName, chatConfig.NearbyConversationIcon),

            ChatChannel.ChatChannelType.USER =>
                CreateUserChannelViewModel(channel, ct),

            ChatChannel.ChatChannelType.COMMUNITY =>
                CreateCommunityChannelViewModel(channel, ct),

            _ => throw new ArgumentOutOfRangeException(nameof(channel.ChannelType), "Unsupported channel type")
        };

        viewModel.UnreadMessagesCount = channel.Messages.Count - channel.ReadMessages;
        return viewModel;
    }

    private UserChannelViewModel CreateUserChannelViewModel(ChatChannel channel, CancellationToken ct)
    {
        var viewModel = new UserChannelViewModel(channel.Id);
        FetchProfileAndUpdateAsync(viewModel, ct).Forget();
        return viewModel;
    }

    private CommunityChannelViewModel CreateCommunityChannelViewModel(ChatChannel channel, CancellationToken ct)
    {
        var viewModel = new CommunityChannelViewModel(channel.Id);
        if (communityDataService.TryGetCommunity(channel.Id, out var communityData))
        {
            viewModel.DisplayName = communityData.name;
            viewModel.ImageUrl = communityData.thumbnails?.raw;
            FetchCommunityThumbnailAndUpdateAsync(viewModel, ct).Forget();
        }

        return viewModel;
    }

    private async UniTaskVoid FetchCommunityThumbnailAndUpdateAsync(CommunityChannelViewModel viewModel, CancellationToken ct)
    {
        var thumbnail = await getCommunityThumbnailCommand
            .ExecuteAsync(viewModel.ImageUrl, ct);

        if (ct.IsCancellationRequested) return;

        viewModel.Thumbnail = thumbnail;

        eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
        {
            ViewModel = viewModel
        });
    }

    private async UniTaskVoid FetchProfileAndUpdateAsync(UserChannelViewModel viewModel, CancellationToken ct)
    {
        var profile = await profileRepository.GetProfileAsync(viewModel.Id.Id, ct);
        if (ct.IsCancellationRequested || profile == null) return;

        viewModel.DisplayName = profile.ValidatedName;
        viewModel.ProfileColor = profile.UserNameColor;
        viewModel.HasClaimedName = profile.HasClaimedName;
        viewModel.IsOnline = true;
        viewModel.ImageUrl = profile.Avatar.FaceSnapshotUrl;

        eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
        {
            ViewModel = viewModel
        });

        await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfilePicture, chatConfig.DefaultProfileThumbnail, profile.UserId, profile.Avatar.FaceSnapshotUrl, ct);
    }
}
