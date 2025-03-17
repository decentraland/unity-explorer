using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace DCL.Chat
{
    public class ChatConversationsToolbarView : MonoBehaviour, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate void ConversationSelectedDelegate(ChatChannel.ChannelId channelId);
        public delegate void ConversationRemovalRequestedDelegate(ChatChannel.ChannelId channelId);

        [SerializeField]
        private RectTransform itemsContainer;

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

        private ViewDependencies viewDependencies;

        private Dictionary<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> items = new ();

        /// <summary>
        ///
        /// </summary>
        public event ConversationSelectedDelegate ConversationSelected;

        /// <summary>
        ///
        /// </summary>
        public event ConversationRemovalRequestedDelegate ConversationRemovalRequested;

        public void SelectConversation(ChatChannel.ChannelId channelId)
        {
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> itemPair in items)
                itemPair.Value.SetSelectionStatus(false);

            items[channelId].SetSelectionStatus(true);

            ConversationSelected?.Invoke(channelId);
        }

        public void AddConversation(ChatChannel channel, Sprite icon = null)
        {
            ChatConversationsToolbarViewItem newItem = Instantiate(itemPrefab, itemsContainer);
            newItem.OpenButtonClicked += OpenButtonClicked;
            newItem.RemoveButtonClicked += OnRemoveButtonClicked;
            newItem.TooltipShown += OnItemTooltipShown;
            newItem.Id = channel.Id;

            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.Nearby:
                    newItem.SetConversationIcon(icon);
                    newItem.SetConversationName("Near By"); // TODO: Localization
                    newItem.SetClaimedNameIconVisibility(false);
                    newItem.SetConnectionStatusVisibility(false);
                    break;
                case ChatChannel.ChatChannelType.User:
                    SetupUserConversationItemAsync(newItem).Forget();
                    break;
                case ChatChannel.ChatChannelType.Community:
                    // TODO in future shapes
                    break;
            }

            newItem.SetConversationType(channel.ChannelType == ChatChannel.ChatChannelType.User);

            items.Add(channel.Id, newItem);

            if(items.Count == 1)
                SelectConversation(channel.Id);

            UpdateScrollButtonsVisibility();
        }

        private void OnItemTooltipShown(GameObject tooltip)
        {
            tooltip.transform.SetParent(transform, true);
        }

        public void RemoveConversation(ChatChannel.ChannelId channelId)
        {
            Destroy(items[channelId].gameObject);
            items.Remove(channelId);
        }

        public void SetUnreadMessages(ChatChannel.ChannelId destinationChannel, int unreadMessages)
        {
            items[destinationChannel].SetUnreadMessages(unreadMessages);
        }

        public void SetConnectionStatus(ChatChannel.ChannelId destinationChannel, OnlineStatus connectionStatus)
        {
            items[destinationChannel].SetConnectionStatus(connectionStatus);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<ChatChannel.ChannelId, ChatConversationsToolbarViewItem> itemPair in items)
                itemPair.Value.HideTooltip(true);

            HideScrollButtons(true);
        }

        private void OpenButtonClicked(ChatConversationsToolbarViewItem item)
        {
            SelectConversation(item.Id);
        }

        private void OnRemoveButtonClicked(ChatConversationsToolbarViewItem item)
        {
            ConversationRemovalRequested?.Invoke(item.Id);
        }

        private async UniTaskVoid SetupUserConversationItemAsync(ChatConversationsToolbarViewItem newItem)
        {
            Profile? profile = await viewDependencies.GetProfileAsync(newItem.Id.Id, CancellationToken.None);

            if (profile != null)
            {
                newItem.SetProfileData(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
                newItem.SetConversationName(profile.ValidatedName);
                newItem.SetClaimedNameIconVisibility(profile.HasClaimedName);
                newItem.SetConnectionStatusVisibility(true);
            }
        }

        public void ShowScrollButtons()
        {
            scrollButtons.gameObject.SetActive(true);
            UpdateScrollButtonsVisibility();
            scrollButtons.DOKill();
            scrollButtons.DOFade(1.0f, 0.5f);
        }

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

        private void OnScrollViewValueChanged(Vector2 arg0)
        {
            UpdateScrollButtonsVisibility();
        }

        private void OnScrollDownButtonClicked()
        {
            scrollView.normalizedPosition = new Vector2(0.0f, scrollView.normalizedPosition.y - scrollView.scrollSensitivity / scrollView.content.sizeDelta.y * 20.0f);
        }

        private void OnScrollUpButtonClicked()
        {
            scrollView.normalizedPosition = new Vector2(0.0f, scrollView.normalizedPosition.y + scrollView.scrollSensitivity / scrollView.content.sizeDelta.y * 20.0f);
        }

        private void UpdateScrollButtonsVisibility()
        {
            scrollUpButton.gameObject.SetActive(scrollView.normalizedPosition.y < 0.9999f);
            scrollDownButton.gameObject.SetActive(scrollView.normalizedPosition.y > 0.0001f);
        }
    }
}
