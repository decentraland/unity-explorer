using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using Utilities;

public class CreateChannelViewModelCommand
{
    private readonly IEventBus eventBus;
    private readonly ChatConfig chatConfig;
    private readonly ProfileRepositoryWrapper profileRepository; 

    public CreateChannelViewModelCommand(
        IEventBus eventBus,
        ChatConfig chatConfig,
        ProfileRepositoryWrapper profileRepository)
    {
        this.eventBus = eventBus;
        this.chatConfig = chatConfig;
        this.profileRepository = profileRepository;
    }

    public ChatChannelViewModel CreateViewModelAndFetch(ChatChannel channel)
    {
        var initialViewModel = new ChatChannelViewModel
        {
            Id = channel.Id,
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

            default:
                initialViewModel.DisplayName = channel.Id.Id;
                break;
        }
        
        return initialViewModel;
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