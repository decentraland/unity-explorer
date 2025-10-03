using DCL.Chat.History;
using DCL.UI;
using DCL.UI.Communities;
using DCL.Utilities;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class CommunityChatConversationsToolbarViewItem : ChatConversationsToolbarViewItem
    {
        private IDisposable? communitySubscription;
        private IDisposable? communityCallIdSubscription;

        private IReadonlyReactiveProperty<string>? currentCommunityCallId;

        [SerializeField] private Sprite listeningToCallIcon  = null!;
        [SerializeField] private Sprite hasCallIcon  = null!;
        [SerializeField] private Image iconImage  = null!;

        public override void SetPicture(Sprite? sprite, Color color)
        {
            customIcon.gameObject.SetActive(false);
            thumbnailView.SetActive(true);

            var communityView = thumbnailView.GetComponent<CommunityThumbnailView>();
            communityView.SetImage(sprite);
        }

        protected override void Start()
        {
            base.Start();
            removeButton.gameObject.SetActive(true);
        }

        public void SetupCommunityUpdates(IReadonlyReactiveProperty<bool> communityUpdates, IReadonlyReactiveProperty<string> communityCallId)
        {
            communitySubscription?.Dispose();
            communitySubscription = communityUpdates.Subscribe(OnCommunityStateUpdated);

            this.currentCommunityCallId = communityCallId;

            communityCallIdSubscription?.Dispose();
            communityCallIdSubscription = communityCallId.Subscribe(OnCommunityCallIdChanged);


            connectionStatusIndicatorContainer.SetActive(communityUpdates.Value);
            if (!communityUpdates.Value) return;
            iconImage.sprite = communityCallId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase)? listeningToCallIcon : hasCallIcon;
        }

        private void OnCommunityCallIdChanged(string currentCallId)
        {
            bool isCurrentCommunity = currentCallId.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase);
            if (isCurrentCommunity)
                connectionStatusIndicatorContainer.SetActive(true);

            iconImage.sprite = isCurrentCommunity? listeningToCallIcon : hasCallIcon;
        }

        private void OnCommunityStateUpdated(bool hasActiveCall)
        {
            connectionStatusIndicatorContainer.SetActive(hasActiveCall);
            if (!hasActiveCall) return;
            if (currentCommunityCallId != null)
                iconImage.sprite = currentCommunityCallId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(Id), StringComparison.InvariantCultureIgnoreCase)?
                    listeningToCallIcon :
                    hasCallIcon;
        }

        //We override it to avoid the original method to change the color or disable the connectionStatusIndicator
        public override void SetConnectionStatus(OnlineStatus connectionStatus)
        {
            //Do nothing.
        }

    }
}
