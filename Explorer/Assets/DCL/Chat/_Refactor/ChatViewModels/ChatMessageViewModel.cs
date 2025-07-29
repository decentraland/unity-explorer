using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMessageViewModel
    {
        internal static readonly ObjectPool<ChatMessageViewModel> POOL = new (
            () => new ChatMessageViewModel(),
            actionOnGet: viewModel => { viewModel.cancellationTokenSource = new CancellationTokenSource(); },
            actionOnRelease: viewModel =>
            {
                viewModel.Message = default(ChatMessage);
                viewModel.ProfileData.ClearSubscriptionsList();
                viewModel.ProfileData.UpdateValue(ProfileThumbnailViewModel.WithColor.Default());
                viewModel.IsSeparator = false;
                viewModel.cancellationTokenSource.SafeCancelAndDispose();
            });

        internal static readonly Action<ChatMessageViewModel> RELEASE = viewModel => POOL.Release(viewModel);

        private CancellationTokenSource cancellationTokenSource;

        public ChatMessage Message { get; internal set; }

        // In case we need more profile information in the future, create a separate ProfileViewModel and update it at once
        public IReactiveProperty<ProfileThumbnailViewModel.WithColor> ProfileData { get; }
            = ProfileThumbnailViewModel.WithColor.DefaultReactive();

        public bool IsSeparator { get; internal set; }

        /// <summary>
        ///     Will be fired when the object is released back to the pool.
        /// </summary>
        internal CancellationToken cancellationToken => cancellationTokenSource.Token;

        private ChatMessageViewModel() { }
    }
}
