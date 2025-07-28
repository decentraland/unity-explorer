using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;

namespace DCL.Chat.ChatUseCases
{
    public class CreateMessageViewModelCommand
    {
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ChatConfig chatConfig;

        public CreateMessageViewModelCommand(ProfileRepositoryWrapper profileRepository, ChatConfig chatConfig)
        {
            this.profileRepository = profileRepository;
            this.chatConfig = chatConfig;
        }

        public ChatMessageViewModel ExecuteForSeparator()
        {
            ChatMessageViewModel? viewModel = ChatMessageViewModel.POOL.Get();
            viewModel.IsSeparator = true;
            return viewModel;
        }

        public ChatMessageViewModel Execute(ChatMessage message)
        {
            ChatMessageViewModel? viewModel = ChatMessageViewModel.POOL.Get();
            viewModel.Message = message;

            if (message.IsSystemMessage)
                viewModel.UserNameColor.UpdateValue(ProfileNameColorHelper.GetNameColor(message.SenderValidatedName));
            else
                FetchProfileAsync(message.SenderWalletAddress, viewModel).Forget();

            return viewModel;
        }

        private async UniTaskVoid FetchProfileAsync(string walletId, ChatMessageViewModel viewModel)
        {
            Profile? profile = await profileRepository.GetProfileAsync(walletId, viewModel.cancellationToken);

            if (profile != null)
            {
                viewModel.UserNameColor.UpdateValue(profile.UserNameColor);

                await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.Thumbnail, chatConfig.DefaultProfileThumbnail,
                    walletId, profile.Avatar.FaceSnapshotUrl, viewModel.cancellationToken);
            }
        }
    }
}
