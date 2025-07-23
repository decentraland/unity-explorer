using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

using Utility;

public class CreateChannelViewModelCommand
{
    private readonly IEventBus eventBus;
    private readonly ICommunityDataService communityDataService;
    private readonly ChatConfig chatConfig;
    private readonly ProfileRepositoryWrapper profileRepository;

    public CreateChannelViewModelCommand(
        IEventBus eventBus,
        ICommunityDataService communityDataService,
        ChatConfig chatConfig,
        ProfileRepositoryWrapper profileRepository)
    {
        this.eventBus = eventBus;
        this.communityDataService = communityDataService;
        this.chatConfig = chatConfig;
        this.profileRepository = profileRepository;
    }

    public ChatChannelViewModel CreateViewModelAndFetch(ChatChannel channel)
    {
        var initialViewModel = new ChatChannelViewModel
        {
            Id = channel.Id, ChannelType = channel.ChannelType,
            UnreadMessagesCount = channel.Messages.Count - channel.ReadMessages,
            IsSelected = false,
            IsDirectMessage = channel.ChannelType == ChatChannel.ChatChannelType.USER
        };

        switch (channel.ChannelType)
        {
            case ChatChannel.ChatChannelType.NEARBY:
                initialViewModel.DisplayName = chatConfig.NearbyConversationName;
                initialViewModel.FallbackIcon = chatConfig.NearbyConversationIcon;
                initialViewModel.IsOnline = true;
                break;

            case ChatChannel.ChatChannelType.USER:

                initialViewModel.DisplayName = "Loading...";
                FetchProfileAndUpdateAsync(initialViewModel, channel, CancellationToken.None).Forget();
                break;

            case ChatChannel.ChatChannelType.COMMUNITY:
                initialViewModel.DisplayName = "Loading...";
                FetchCommunityDataAndUpdate(initialViewModel, channel.Id);
                break;
            
            default:
                initialViewModel.DisplayName = channel.Id.Id;
                break;
        }

        return initialViewModel;
    }

    private void FetchCommunityDataAndUpdate(ChatChannelViewModel viewModel, ChatChannel.ChannelId channelId)
    {
        // Get the data from our cache service
        if (communityDataService.TryGetCommunity(channelId, out var communityData))
        {
            viewModel.DisplayName = communityData.name;
            viewModel.ImageUrl = communityData.thumbnails?.raw;
            viewModel.IsOnline = true;

            // Since this is a synchronous update from cached data, we can publish the event immediately.
            // No async operation is needed here, which simplifies things.
            eventBus.Publish(new ChatEvents.ChannelUpdatedEvent
            {
                ViewModel = viewModel
            });
        }
        else
        {
            // This case might happen if a community channel was created from an event
            // before its data was fully fetched. The UI will show "Loading..."
            // and we can add a mechanism to retry fetching later.
            // For now, we can just log it.
            ReportHub.LogWarning(ReportCategory.COMMUNITIES, $"Could not find community data for channel {channelId.Id}");
        }
    }
    
    private async UniTaskVoid FetchProfileAndUpdateAsync(ChatChannelViewModel viewModel, ChatChannel channel, CancellationToken ct)
    {
        Profile? profile = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
        if (ct.IsCancellationRequested || profile == null) return;

        viewModel.DisplayName = profile.ValidatedName;
        viewModel.ImageUrl = profile.Avatar.FaceSnapshotUrl;
        viewModel.ProfileColor = profile.UserNameColor;
        viewModel.HasClaimedName = profile.HasClaimedName;
        viewModel.IsOnline = true;
        // You would get IsOnline from another service, e.g., ChatUserStateUpdater
        // viewModel.IsOnline = ...

        eventBus.Publish(new ChatEvents.ChannelUpdatedEvent { ViewModel = viewModel });
    }
}
