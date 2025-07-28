using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;

using Utility;

namespace DCL.Chat.ChatUseCases
{
    public class GetTitlebarViewModelCommand
    {
        private readonly IEventBus eventBus;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ChatConfig chatConfig;

        public GetTitlebarViewModelCommand(
            IEventBus eventBus,
            ProfileRepositoryWrapper profileRepository,
            ChatConfig chatConfig)
        {
            this.eventBus = eventBus;
            this.profileRepository = profileRepository;
            this.chatConfig = chatConfig;
        }

        public async UniTask<ChatTitlebarViewModel> ExecuteAsync(ChatChannel channel, CancellationToken ct)
        {
            var viewModel = new ChatTitlebarViewModel();

            if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
            {
                viewModel.ViewMode = Mode.DirectMessage;
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
                viewModel.ViewMode = Mode.Nearby;
                viewModel.Username = chatConfig.NearbyConversationName;
                viewModel.HasClaimedName = false;
                viewModel.Thumbnail.UpdateValue(ProfileThumbnailViewModel.FromLoaded(chatConfig.NearbyConversationIcon, true));
            }

            return viewModel;
        }
    }
}
