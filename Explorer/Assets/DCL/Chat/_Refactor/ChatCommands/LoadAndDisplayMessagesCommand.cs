using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using Utility;


namespace DCL.Chat.ChatUseCases
{
    /// <summary>
    ///     This command acts as a high-level orchestrator. It fetches the initial list of
    ///     ChatMessageViewModels from the history and then kicks off the asynchronous process
    ///     to load profile thumbnails, publishing events as they complete.
    /// </summary>
    public class LoadAndDisplayMessagesCommand
    {
        private readonly IEventBus eventBus;
        private readonly GetMessageHistoryCommand getMessageHistoryCommand;
        private readonly GetProfileThumbnailCommand getProfileThumbnailCommand;

        public LoadAndDisplayMessagesCommand(
            IEventBus eventBus,
            GetMessageHistoryCommand getMessageHistoryCommand,
            GetProfileThumbnailCommand getProfileThumbnailCommand)
        {
            this.eventBus = eventBus;
            this.getMessageHistoryCommand = getMessageHistoryCommand;
            this.getProfileThumbnailCommand = getProfileThumbnailCommand;
        }

        public async UniTask<List<ChatMessageViewModel>> ExecuteAsync(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            var result = await getMessageHistoryCommand.ExecuteAsync(channelId, ct);

            if (ct.IsCancellationRequested)
                return new List<ChatMessageViewModel>();

            var viewModels = result.ViewModelMessages;

            foreach (var vm in viewModels)
            {
                if (!string.IsNullOrEmpty(vm.FaceSnapshotUrl))
                    FetchThumbnailAndUpdateAsync(vm, ct).Forget();
            }

            return viewModels;
        }

        private async UniTaskVoid FetchThumbnailAndUpdateAsync(ChatMessageViewModel viewModel, CancellationToken ct)
        {
            var thumbnail = await getProfileThumbnailCommand.ExecuteAsync(viewModel.SenderWalletAddress, viewModel.FaceSnapshotUrl, ct);

            if (ct.IsCancellationRequested) return;

            viewModel.ProfilePicture = thumbnail;
            viewModel.IsLoadingPicture = false;

            eventBus.Publish(new ChatEvents.ChatMessageUpdatedEvent
            {
                ViewModel = viewModel
            });
        }
    }
}