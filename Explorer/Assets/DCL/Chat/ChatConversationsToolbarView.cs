using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Utility;

namespace DCL.Chat
{
    public class ChatConversationsToolbarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate void ConversationSelectedDelegate(ChatChannel.ChannelId channelId);
        public delegate void ConversationRemovalRequestedDelegate(ChatChannel.ChannelId channelId);

        [SerializeField]
        private RectTransform itemsContainer;

        [SerializeField]
        private ChatConversationsToolbarViewItem nearbyConversationItemPrefab;

        [SerializeField]
        private PrivateChatConversationsToolbarViewItem privateConversationItemPrefab;

        [SerializeField]
        private CommunityChatConversationsToolbarViewItem communityConversationItemPrefab;

        [SerializeField]
        private CanvasGroup scrollButtons;

        [SerializeField]
        private ScrollRect scrollView;

        [SerializeField]
        private Button scrollUpButton;

        [SerializeField]
        private Button scrollDownButton;

        private const string NEARBY_CONVERSATION_NAME = "Nearby";

        private Dictionary<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> items = new ();
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        /// <summary>
        /// Raised when a different conversation item is selected.
        /// </summary>
        public event ConversationSelectedDelegate ConversationSelected;

        /// <summary>
        /// Raised when the user requests to remove a conversation.
        /// </summary>
        public event ConversationRemovalRequestedDelegate ConversationRemovalRequested;

        /// <summary>
        /// Marks an item as selected.
        /// </summary>
        /// <param name="channelId">The Id of the conversation to find the item.</param>
        public void SelectConversation(ChatChannel.ChannelId channelId)
        {
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> itemPair in items)
                itemPair.Value.SetSelectionStatus(false);

            items[channelId].SetSelectionStatus(true);

            ConversationSelected?.Invoke(channelId);
        }

        /// <summary>
        /// Creates a new item and adds it to the last position of the toolbar. It does not change any data.
        /// </summary>
        /// <param name="channel">The channel the item will represent.</param>
        public void AddConversation(ChatChannel channel)
        {
            ChatConversationsToolbarViewItem newItem;

            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    newItem = Instantiate(nearbyConversationItemPrefab, itemsContainer);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    newItem = Instantiate(communityConversationItemPrefab, itemsContainer);
                    break;
                case ChatChannel.ChatChannelType.USER:
                    newItem = Instantiate(privateConversationItemPrefab, itemsContainer);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            newItem.Initialize();
            newItem.OpenButtonClicked += OpenButtonClicked;
            newItem.RemoveButtonClicked += OnRemoveButtonClicked;
            newItem.TooltipShown += OnItemTooltipShown;
            newItem.Id = channel.Id;

            items.Add(channel.Id, newItem);

            if(items.Count == 1)
                SelectConversation(channel.Id);

            UpdateScrollButtonsVisibility();
        }

        public void SetNearbyConversationData(Sprite icon)
        {
            ChatConversationsToolbarViewItem conversationItem = items[ChatChannel.NEARBY_CHANNEL_ID];
            conversationItem.SetConversationIcon(icon);
            conversationItem.SetConversationName(NEARBY_CONVERSATION_NAME); // TODO: Localization
            conversationItem.SetClaimedNameIconVisibility(false);
        }

        public void SetCommunityConversationData(ChatChannel.ChannelId channelId, ISpriteCache thumbnailCache, GetUserCommunitiesData.CommunityData communityData, CancellationToken ct)
        {
            CommunityChatConversationsToolbarViewItem conversationItem = (CommunityChatConversationsToolbarViewItem)items[channelId];
            SetupCommunityConversationItem(conversationItem, communityData, thumbnailCache, ct);
        }

        public void SetPrivateConversationData(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            PrivateChatConversationsToolbarViewItem conversationItem = (PrivateChatConversationsToolbarViewItem)items[channelId];
            SetupPrivateConversationItemAsync(conversationItem, ct).Forget();
        }

        /// <summary>
        /// Removes a conversation item from the toolbar UI. It does not change any data.
        /// </summary>
        /// <param name="channelId">The Id of the conversation to find the item.</param>
        public void RemoveConversation(ChatChannel.ChannelId channelId)
        {
            Destroy(items[channelId].gameObject);
            items.Remove(channelId);
            UpdateScrollButtonsVisibility();
        }

        /// <summary>
        /// Removes all conversation items from the toolbar UI. It does not change any data.
        /// </summary>
        public void RemoveAllConversations()
        {
            foreach (var itemsValue in items.Values)
                UnityObjectUtils.SafeDestroyGameObject(itemsValue);
            items.Clear();
            UpdateScrollButtonsVisibility();
        }

        /// <summary>
        /// Replaces the value of unread messages to show next to the icon of an item.
        /// </summary>
        /// <param name="destinationChannel">The Id of the conversation to find the item.</param>
        /// <param name="unreadMessages">The amount of unread messages in the conversation.</param>
        /// <param name="hasMentions">Whether there are mentions among the unread messages or not.</param>
        public void SetUnreadMessages(ChatChannel.ChannelId destinationChannel, int unreadMessages, bool hasMentions)
        {
            items[destinationChannel].ShowMentionSign(hasMentions);

            if(!hasMentions)
                items[destinationChannel].SetUnreadMessages(unreadMessages);
        }

        /// <summary>
        /// Changes the visual aspect of the connection status of one the items.
        /// </summary>
        /// <param name="destinationChannel">The Id of the conversation to find the item.</param>
        /// <param name="connectionStatus">The current connection status.</param>
        public void SetConnectionStatus(ChatChannel.ChannelId destinationChannel, OnlineStatus connectionStatus)
        {
            if (items[destinationChannel] != null)
                items[destinationChannel].SetConnectionStatus(connectionStatus);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="profileRepositoryWrapper"></param>
        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            this.profileRepositoryWrapper = profileDataProvider;
        }

        /// <summary>
        /// Makes one or both scroll buttons appear (depending on the current scroll position), or none of them if there are not enough items in the toolbar.
        /// </summary>
        public void ShowScrollButtons()
        {
            scrollButtons.gameObject.SetActive(true);
            UpdateScrollButtonsVisibility();
            scrollButtons.DOKill();
            scrollButtons.DOFade(1.0f, 0.5f);
        }

        /// <summary>
        /// Makes both scroll buttons disappear with or without animations.
        /// </summary>
        /// <param name="isImmediate">Whether to skip animations or not.</param>
        public void HideScrollButtons(bool isImmediate)
        {
            if (scrollButtons.gameObject.activeSelf)
            {
                if (isImmediate)
                {
                    scrollButtons.gameObject.SetActive(false);
                }
                else
                {
                    scrollButtons.DOKill();
                    scrollButtons.DOFade(0.0f, 0.5f).OnComplete( () => scrollButtons.gameObject.SetActive(false) );
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowScrollButtons();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideScrollButtons(false);
        }

        private void Start()
        {
            scrollUpButton.onClick.AddListener(OnScrollUpButtonClicked);
            scrollDownButton.onClick.AddListener(OnScrollDownButtonClicked);
            scrollView.onValueChanged.AddListener(OnScrollViewValueChanged);
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> itemPair in items)
                itemPair.Value.HideTooltip(true);

            HideScrollButtons(true);
        }

        private void OnScrollViewValueChanged(Vector2 _)
        {
            UpdateScrollButtonsVisibility();
        }

        private void OnScrollDownButtonClicked()
        {
            scrollView.normalizedPosition = new Vector2(0.0f, scrollView.normalizedPosition.y - scrollView.scrollSensitivity / scrollView.content.sizeDelta.y * 50.0f);
        }

        private void OnScrollUpButtonClicked()
        {
            scrollView.normalizedPosition = new Vector2(0.0f, scrollView.normalizedPosition.y + scrollView.scrollSensitivity / scrollView.content.sizeDelta.y * 50.0f);
        }

        private void UpdateScrollButtonsVisibility()
        {
            if (scrollView.content.rect.height > scrollView.viewport.rect.height)
            {
                // It may show one or both
                scrollUpButton.gameObject.SetActive(scrollView.normalizedPosition.y < 0.9999f);
                scrollDownButton.gameObject.SetActive(scrollView.normalizedPosition.y > 0.0001f);
            }
            else
            {
                // Hidden if there is no scroll to be done
                scrollUpButton.gameObject.SetActive(false);
                scrollDownButton.gameObject.SetActive(false);
            }
        }

        private void OpenButtonClicked(ChatConversationsToolbarViewItem item)
        {
            SelectConversation(item.Id);
        }

        private void OnRemoveButtonClicked(ChatConversationsToolbarViewItem item)
        {
            ConversationRemovalRequested?.Invoke(item.Id);
        }

        private void OnItemTooltipShown(GameObject tooltip)
        {
            tooltip.transform.SetParent(transform, true);
        }

        private async UniTaskVoid SetupPrivateConversationItemAsync(PrivateChatConversationsToolbarViewItem newItem, CancellationToken ct)
        {
            Profile? profile = await profileRepositoryWrapper.GetProfileAsync(newItem.Id.Id, ct);

            if (profile != null)
            {
                newItem.SetProfileData(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                newItem.SetConversationName(profile.ValidatedName);
                newItem.SetClaimedNameIconVisibility(profile.HasClaimedName);
            }
        }

        private void SetupCommunityConversationItem(CommunityChatConversationsToolbarViewItem newItem, GetUserCommunitiesData.CommunityData communityData, ISpriteCache thumbnailCache, CancellationToken ct)
        {
            newItem.SetThumbnailData(thumbnailCache, communityData.thumbnails?.raw, ct);
            newItem.SetConversationName(communityData.name);
        }

        /// <summary>
        /// Moves the icon that corresponds to a conversation to the given position in the list of conversations.
        /// </summary>
        /// <param name="channelToMove">The conversation to move.</param>
        /// <param name="position">The index of the new position.</param>
        public void MoveConversationToPosition(ChatChannel.ChannelId channelToMove, int position)
        {
            items[channelToMove].transform.SetSiblingIndex(position);
        }
    }
}
