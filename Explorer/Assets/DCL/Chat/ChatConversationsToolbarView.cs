using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Profiles;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatConversationsToolbarView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public delegate void ConversationSelectedDelegate(ChatChannel.ChannelId channelId);
        public delegate void ConversationRemovalRequestedDelegate(ChatChannel.ChannelId channelId);

        [SerializeField]
        private RectTransform itemsContainer;

        [SerializeField]
        private ChatConversationsToolbarViewItem itemPrefab;

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
            newItem.Id = channel.Id;

            if (icon != null)
            {
                newItem.SetConversationIcon(icon);
            }
            else
            {
                UniTask.RunOnThreadPool(() => LoadProfileAsync(newItem)).Forget();
            }

            newItem.SetConversationName(newItem.Id.Id);
            newItem.SetConversationType(channel.ChannelType == ChatChannel.ChatChannelType.User);

            items.Add(channel.Id, newItem);

            if(items.Count == 1)
                SelectConversation(channel.Id);
        }

        public void RemoveConversation(ChatChannel.ChannelId channelId)
        {
            Destroy(items[channelId]);
            items.Remove(channelId);
        }

        public void SetUnreadMessages(ChatChannel.ChannelId destinationChannel, int unreadMessages)
        {
            items[destinationChannel].SetUnreadMessages(unreadMessages);
        }

        public void SetConnectionStatus(ChatChannel.ChannelId destinationChannel, bool isConnected)
        {
            items[destinationChannel].SetConnectionStatus(isConnected);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void OpenButtonClicked(ChatConversationsToolbarViewItem item)
        {
            SelectConversation(item.Id);
        }

        private void OnRemoveButtonClicked(ChatConversationsToolbarViewItem item)
        {
            ConversationRemovalRequested?.Invoke(item.Id);
        }

        private async UniTaskVoid LoadProfileAsync(ChatConversationsToolbarViewItem newItem)
        {
            Profile? profile = await viewDependencies.GetProfileAsync(newItem.Id.Id, CancellationToken.None);

            if (profile != null)
                newItem.SetProfileData(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
        }
    }
}
