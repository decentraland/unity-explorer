using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Chat
{
    internal class ChatConversationsToolbarViewItemPool
    {
        private readonly RectTransform itemsContainer;
        private readonly Dictionary<Type, (object Pool, Action<object> ReleaseAction)> poolRegistry = new ();

        public T Get<T>() where T : ChatConversationsToolbarViewItem
        {
            if (poolRegistry.TryGetValue(typeof(T), out (object Pool, Action<object> ReleaseAction) poolHandle))
                return ((ObjectPool<T>) poolHandle.Pool).Get();

            throw new Exception($"No pool found for type {typeof(T)}. Make sure to register it in the constructor.");
        }

        public void Release<T>(T item) where T : ChatConversationsToolbarViewItem
        {
            if (poolRegistry.TryGetValue(item.GetType(), out var poolHandle))
            {
                poolHandle.ReleaseAction.Invoke(item);
                return;
            }

            throw new Exception($"No pool found for type {item.GetType()}. Make sure to register it in the constructor.");
        }

        private void CreateObjectPool<T>(T prefab) where T: ChatConversationsToolbarViewItem
        {
            ObjectPool<T> pool = new (
                createFunc: () => Object.Instantiate(prefab, itemsContainer),
                actionOnGet: component => component.gameObject.SetActive(true),
                actionOnRelease: component => component?.gameObject.SetActive(false),
                actionOnDestroy: component => Object.Destroy(component.gameObject));

            Type type = typeof(T);
            poolRegistry[type] = (pool, obj => pool.Release((T)obj));
        }

        public ChatConversationsToolbarViewItemPool(
            RectTransform itemsContainer,
            ChatConversationsToolbarViewItem nearbyConversationItemPrefab,
            PrivateChatConversationsToolbarViewItem privateConversationItemPrefab,
            CommunityChatConversationsToolbarViewItem communityConversationItemPrefab)
        {
            this.itemsContainer = itemsContainer;

            CreateObjectPool(nearbyConversationItemPrefab);
            CreateObjectPool(privateConversationItemPrefab);
            CreateObjectPool(communityConversationItemPrefab);
        }
    }
}
