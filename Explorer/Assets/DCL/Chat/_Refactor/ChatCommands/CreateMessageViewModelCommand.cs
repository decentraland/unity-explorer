using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Translation.Service;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System.Threading;
using DCL.Translation;

namespace DCL.Chat.ChatCommands
{
    public class CreateMessageViewModelCommand
    {
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly ITranslationMemory translationMemory;

        public CreateMessageViewModelCommand(ProfileRepositoryWrapper profileRepository,
            ChatConfig.ChatConfig chatConfig,
            ITranslationMemory translationMemory)
        {
            this.profileRepository = profileRepository;
            this.chatConfig = chatConfig;
            this.translationMemory = translationMemory;
        }

        public ChatMessageViewModel ExecuteForSeparator()
        {
            ChatMessageViewModel? viewModel = ChatMessageViewModel.POOL.Get();
            viewModel.IsSeparator = true;
            return viewModel;
        }

        public ChatMessageViewModel Execute(ChatMessage message, ChatMessage? previousMessage, bool isTopMostInTheFeed)
        {
            ChatMessageViewModel? viewModel = ChatMessageViewModel.POOL.Get();
            viewModel.Message = message;

            // Whether the timestamp is not null (old messages, backward compatibility),
            // it's not the last padding message, and either the message is the first in the feed or the day it was sent is different from the previous messages
            viewModel.ShowDateDivider = message.SentTimestamp.HasValue &&
                                        ((previousMessage != null && message.SentTimestamp.Value.Date != previousMessage.Value.SentTimestamp?.Date)
                                         || isTopMostInTheFeed);

            if (translationMemory.TryGet(message.MessageId, out var translation))
            {
                viewModel.TranslationState = translation.State;
                viewModel.TranslatedText = translation.TranslatedBody;
                viewModel.TranslationError = string.Empty;
            }
            else
            {
                // No translation state was found (it was evicted or never existed).
                // Explicitly set the ViewModel to its default, original state (it's already
                // reset when view model is released but to make it explicit here).
                viewModel.TranslationState = TranslationState.Original;
                viewModel.TranslatedText = string.Empty;
                viewModel.TranslationError = string.Empty;
            }

            if (message.IsSystemMessage)
                viewModel.ProfileData.UpdateValue(new ProfileThumbnailViewModel.WithColor(ProfileThumbnailViewModel.FromLoaded(chatConfig.NearbyConversationIcon, true), NameColorHelper.GetNameColor(message.SenderValidatedName)));
            else
                FetchProfileAsync(message.SenderWalletAddress, viewModel).Forget();

            return viewModel;
        }

        /// <summary>
        ///     Function can be cancelled at any time as the view model is released back to the pool:
        ///     switching between channels, closing the chat, etc.
        /// </summary>
        private async UniTaskVoid FetchProfileAsync(string walletId, ChatMessageViewModel viewModel)
        {
            CancellationToken cancellationToken = viewModel.cancellationToken;

            Result<Profile?> profileResult = await profileRepository.GetProfileAsync(walletId, cancellationToken).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (!profileResult.Success)
                return;

            Profile? profile = profileResult.Value;

            if (profile != null)
            {
                viewModel.ProfileData.UpdateValue(viewModel.ProfileData.Value.SetColor(profile.UserNameColor));

                await GetProfileThumbnailCommand.Instance.ExecuteAsync(viewModel.ProfileData, chatConfig.DefaultProfileThumbnail,
                    walletId, profile.Avatar.FaceSnapshotUrl, cancellationToken);
            }
            else
            {
                viewModel.ProfileData.UpdateValue(new ProfileThumbnailViewModel.WithColor(ProfileThumbnailViewModel.FromFallback(chatConfig.DefaultProfileThumbnail),
                    ProfileThumbnailViewModel.WithColor.DEFAULT_PROFILE_COLOR));
            }
        }
    }
}
