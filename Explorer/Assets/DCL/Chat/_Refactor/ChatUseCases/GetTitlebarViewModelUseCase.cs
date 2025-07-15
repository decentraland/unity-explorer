using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using Utilities;

namespace DCL.Chat.ChatUseCases
{
    public class GetTitlebarViewModelUseCase
    {
        private readonly IEventBus eventBus;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly GetProfileThumbnailUseCase getThumbnailUseCase;
        private readonly ChatConfig chatConfig;

        public GetTitlebarViewModelUseCase(
            IEventBus eventBus,
            ProfileRepositoryWrapper profileRepository,
            GetProfileThumbnailUseCase getThumbnailUseCase,
            ChatConfig chatConfig)
        {
            this.eventBus = eventBus;
            this.profileRepository = profileRepository;
            this.getThumbnailUseCase = getThumbnailUseCase;
            this.chatConfig = chatConfig;
        }

        public async UniTask<ChatTitlebarViewModel> ExecuteAsync(ChatChannel channel, CancellationToken ct)
        {
            var viewModel = new ChatTitlebarViewModel();

            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                viewModel.ViewMode = Mode.DirectMessage;
                viewModel.UserProfileId = new Web3Address(channel.Id.Id);

                Profile? profile = await profileRepository.GetProfileAsync(channel.Id.Id, ct);
                if (ct.IsCancellationRequested || profile == null)
                {
                    return new ChatTitlebarViewModel
                    {
                        Title = "Unknown User"
                    };
                }

                viewModel.Title = profile.ValidatedName;
                viewModel.HasClaimedName = profile.HasClaimedName;
                viewModel.ProfileColor = profile.UserNameColor;
                viewModel.ProfileSprite = await getThumbnailUseCase.ExecuteAsync(
                    profile.UserId,
                    profile.Avatar.FaceSnapshotUrl,
                    ct
                );
            }
            else
            {
                viewModel.ViewMode = Mode.Nearby;
                viewModel.Title = chatConfig.NearbyConversationName;
                viewModel.HasClaimedName = false;
                viewModel.ProfileSprite = chatConfig.NearbyConversationIcon;
            }

            return viewModel;
        }
    }
}