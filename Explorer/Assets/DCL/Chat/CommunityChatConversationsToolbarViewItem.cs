using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.Communities;
using DCL.Utilities;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class CommunityChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        private IDisposable? communitySubscription;
        private IDisposable? communityCallIdSubscription;

        private IReadonlyReactiveProperty<string> currentCommunityCallId;

        [field: SerializeField] private Sprite ListeningToCallIcon;
        [field: SerializeField] private Sprite HasCallIcon;
        [field: SerializeField] private Image IconImage;

        /// <summary>
        /// Provides the data required to display the community thumbnail.
        /// </summary>
        /// <param name="thumbnailCache">A way to access thumbnail images asynchronously.</param>
        /// <param name="imageUrl">The URL to the thumbnail picture.</param>
        /// <param name="ct">A cancellation token for the thumbnail download operation.</param>
        public void SetThumbnailData(ISpriteCache thumbnailCache, string? imageUrl, CancellationToken ct)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            if(imageUrl != null)
                thumbnailView.GetComponent<CommunityThumbnailView>().LoadThumbnailAsync(thumbnailCache, imageUrl, ct).Forget();
        }

        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
        }

        public void SetupCommunityUpdates(ReactiveProperty<bool> communityUpdates, IReadonlyReactiveProperty<string> currentCommunityCallId)
        {
            communitySubscription?.Dispose();
            communitySubscription = communityUpdates.Subscribe(OnCommunityStateUpdated);

            this.currentCommunityCallId = currentCommunityCallId;

            communityCallIdSubscription?.Dispose();
            communityCallIdSubscription = currentCommunityCallId.Subscribe(OnCommunityCallIdChanged);


            connectionStatusIndicatorContainer.SetActive(communityUpdates.Value);
            if (!communityUpdates.Value) return;
            IconImage.sprite = currentCommunityCallId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase)? ListeningToCallIcon : HasCallIcon;
        }

        private void OnCommunityCallIdChanged(string currentCallId)
        {
            IconImage.sprite = currentCallId.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase)? ListeningToCallIcon : HasCallIcon;
        }

        private void OnCommunityStateUpdated(bool hasActiveCall)
        {
            connectionStatusIndicatorContainer.SetActive(hasActiveCall);
            if (!hasActiveCall) return;

            IconImage.sprite = currentCommunityCallId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase)? ListeningToCallIcon : HasCallIcon;
        }
    }
}
