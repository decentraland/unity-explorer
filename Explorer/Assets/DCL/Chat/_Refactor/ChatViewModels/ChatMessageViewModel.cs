using DCL.Chat.History;
using DCL.Translation;

using DCL.UI.ProfileElements;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMessageViewModel
    {
        internal static readonly ObjectPool<ChatMessageViewModel> POOL = new (
            () => new ChatMessageViewModel(),
            actionOnGet: viewModel => { viewModel.cancellationTokenSource = viewModel.cancellationTokenSource.SafeRestart(); },
            actionOnRelease: viewModel =>
            {
                viewModel.Message = default(ChatMessage);
                viewModel.ProfileData.ClearSubscriptionsList();
                viewModel.ProfileData.UpdateValue(ProfileThumbnailViewModel.Default());
                viewModel.ProfileOptionalBasicInfo.ClearSubscriptionsList();
                viewModel.ProfileOptionalBasicInfo.UpdateValue(UI.ProfileElements.ProfileOptionalBasicInfo.Default());
                viewModel.IsSeparator = false;
                viewModel.cancellationTokenSource.SafeCancelAndDispose();
                viewModel.PendingToAnimate = false;
                viewModel.ShowDateDivider = false;
                viewModel.TranslationState = TranslationState.Original;
                viewModel.TranslatedText = string.Empty;
                viewModel.TranslationError = string.Empty;
                viewModel.Reactions = null;
            });

        internal static readonly Action<ChatMessageViewModel> RELEASE = viewModel => POOL.Release(viewModel);

        private CancellationTokenSource cancellationTokenSource = new ();

        // Monotonic content-version counter, bumped by every setter that feeds a bound cell's visuals
        // (Message/TranslationState/TranslatedText/ShowDateDivider/Reactions). ChatEntryView caches the
        // last-bound (reference, Version) pair and skips the expensive rebuild when the SAME pooled view
        // model is re-offered unchanged (RefreshAllShownItem). INVARIANT: any future mutable field whose
        // value SetItemData renders MUST bump Version here, or a gated cell will show stale content.
        public int Version { get; private set; }

        private ChatMessage message;
        public ChatMessage Message { get => message; internal set { message = value; Version++; } }

        private bool showDateDivider;
        public bool ShowDateDivider { get => showDateDivider; internal set { showDateDivider = value; Version++; } }

        private TranslationState translationState = TranslationState.Original;
        public TranslationState TranslationState { get => translationState; set { translationState = value; Version++; } }

        private string translatedText = string.Empty;
        public string TranslatedText { get => translatedText; set { translatedText = value; Version++; } }

        // Deliberately does NOT bump Version: nothing renders this field (SetItemData never reads it),
        // and every write pairs with a TranslationState/TranslatedText change that already bumps Version.
        public string TranslationError { get; set; } = string.Empty;

        public bool IsTranslated => TranslationState == TranslationState.Success;
        public string DisplayText => GetDisplayText();

        private string GetDisplayText()
        {
            return TranslationState switch
            {
                TranslationState.Success => TranslatedText,
                // We don't need a "Pending" text; the view will handle the visual effect
                _ => Message.Message
            };
        }

        // In case we need more profile information in the future, create a separate ProfileViewModel and update it at once
        public IReactiveProperty<ProfileThumbnailViewModel> ProfileData { get; }
            = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());

        public IReactiveProperty<ProfileOptionalBasicInfo> ProfileOptionalBasicInfo { get; }
            = new ReactiveProperty<ProfileOptionalBasicInfo>(UI.ProfileElements.ProfileOptionalBasicInfo.Default());

        public bool IsSeparator { get; internal set; }

        public bool PendingToAnimate { get; internal set; }

        private ReactionSet? reactions;
        public ReactionSet? Reactions { get => reactions; internal set { reactions = value; Version++; } }

        /// <summary>
        ///     Will be fired when the object is released back to the pool.
        /// </summary>
        internal CancellationToken cancellationToken => cancellationTokenSource.Token;

        private ChatMessageViewModel() { }
    }
}
