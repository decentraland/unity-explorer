using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatChannelsView : MonoBehaviour, IChatChannelsView,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField]
        private RectTransform itemsContainer;
        public RectTransform ItemsContainer => itemsContainer;

        [SerializeField]
        private CanvasGroup conversationsToolbarCanvasGroup;
        
        [SerializeField]
        private ChatConversationsToolbarViewItem itemPrefab;

        [SerializeField]
        private CanvasGroup scrollButtons;

        [SerializeField]
        private ScrollRect scrollView;

        [SerializeField]
        private Button scrollUpButton;

        [SerializeField]
        private Button scrollDownButton;

        private Dictionary<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> items = new ();
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        
        public event Action<ChatChannel.ChannelId> ConversationSelected;
        public event Action<ChatChannel.ChannelId> ConversationRemovalRequested;

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
        public void SetUnreadMessages(ChatChannel.ChannelId destinationChannel, int unreadMessages)
        {
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

        public void Initialize(ProfileRepositoryWrapper profileRepo)
        {
            this.profileRepositoryWrapper = profileRepo;
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

        // private async UniTaskVoid SetupUserConversationItemAsync(ChatConversationsToolbarViewItem newItem)
        // {
        //     Profile? profile = await profileRepositoryWrapper.GetProfileAsync(newItem.Id.Id, CancellationToken.None);
        //
        //     if (profile != null)
        //     {
        //         newItem.SetProfileData(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
        //         newItem.SetConversationName(profile.ValidatedName);
        //         newItem.SetClaimedNameIconVisibility(profile.HasClaimedName);
        //         newItem.SetConversationType(true);
        //     }
        // }

        public event Action<string>? OnChannelSelected;
        public event Action<string>? OnChannelRemoved;

        public void AddConversation(ChatChannelViewModel viewModel)
        {
            var newItem = Instantiate(itemPrefab, itemsContainer);
            newItem.Initialize();
            newItem.Id = viewModel.Id;
            
            newItem.OpenButtonClicked += OpenButtonClicked;
            newItem.RemoveButtonClicked += OnRemoveButtonClicked;
            newItem.TooltipShown += OnItemTooltipShown;
            
            newItem.SetConversationName(viewModel.DisplayName);
            newItem.SetUnreadMessages(viewModel.UnreadMessagesCount);
            newItem.SetConversationType(viewModel.IsDirectMessage);
            newItem.SetClaimedNameIconVisibility(viewModel.HasClaimedName);
            newItem.SetConnectionStatus(viewModel.IsOnline ? OnlineStatus.ONLINE : OnlineStatus.OFFLINE);
            
            if (viewModel.IsDirectMessage && !string.IsNullOrEmpty(viewModel.ImageUrl))
            {
                newItem.SetProfileData(
                    profileRepositoryWrapper,
                    viewModel.ProfileColor,
                    viewModel.ImageUrl,
                    viewModel.Id.Id
                );
            }
            else
            {
                // We are using a fallback icon (e.g., for Nearby).
                newItem.SetConversationIcon(viewModel.FallbackIcon);
            }
            
            items.Add(viewModel.Id, newItem);
            UpdateScrollButtonsVisibility();
        }

        public void RemoveConversation(ChatChannel channel)
        {
            if (items.TryGetValue(channel.Id, out var itemToRemove))
            {
                UnityObjectUtils.SafeDestroyGameObject(itemToRemove);
                items.Remove(channel.Id);
                UpdateScrollButtonsVisibility(); // Refresh scrollbar state
            } 
        }

        public void SetUnreadMessages(string channelId, int count)
        {
            
        }

        public void UpdateConversation(ChatChannelViewModel viewModel)
        {
            if (items.TryGetValue(viewModel.Id, out var itemToUpdate))
            {
                itemToUpdate.SetConversationName(viewModel.DisplayName);
                itemToUpdate.SetClaimedNameIconVisibility(viewModel.HasClaimedName);
            
                if (viewModel.IsDirectMessage && !string.IsNullOrEmpty(viewModel.ImageUrl))
                {
                    itemToUpdate.SetProfileData(
                        profileRepositoryWrapper,
                        viewModel.ProfileColor,
                        viewModel.ImageUrl,
                        viewModel.Id.Id
                    );
                }
            }
        }

        public void SetOnlineStatus(string channelId, bool isOnline)
        {
            if (items.TryGetValue(new ChatChannel.ChannelId(channelId), out var item))
            {
                item.SetConnectionStatus(isOnline ? OnlineStatus.ONLINE : OnlineStatus.OFFLINE);
            }
        }

        public void AddItem(ChatConversationsToolbarViewItem newItem)
        {
            if (!items.TryAdd(newItem.Id, newItem)) return;
            
            newItem.OpenButtonClicked += OpenButtonClicked;
            newItem.RemoveButtonClicked += OnRemoveButtonClicked;
            newItem.TooltipShown += OnItemTooltipShown;
            
            if (items.Count == 1)
            {
                SelectConversation(newItem.Id);
            }
        }

        public void Clear()
        {
            items.Clear();
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Sets the visual focus state for the conversations toolbar.
        /// </summary>
        public void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing)
        {
            conversationsToolbarCanvasGroup.DOKill();

            float targetAlpha = isFocused ? 1.0f : 0.0f;
            float fadeDuration = animate ? duration : 0f;

            if (isFocused && !conversationsToolbarCanvasGroup.gameObject.activeSelf)
            {
                conversationsToolbarCanvasGroup.gameObject.SetActive(true);
            }

            conversationsToolbarCanvasGroup.DOFade(targetAlpha, fadeDuration)
                .SetEase(easing)
                .OnComplete(() =>
                {
                    if (!isFocused)
                    {
                        conversationsToolbarCanvasGroup.gameObject.SetActive(false);
                    }
                });
        }
    }
}