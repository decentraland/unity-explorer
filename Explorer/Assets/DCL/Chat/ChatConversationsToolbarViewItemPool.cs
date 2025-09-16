using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat
{
    internal class ChatConversationsToolbarViewItemPool
    {
        private const string ROOT_POOL_CONTAINER_NAME = "ROOT_POOL_CONTAINER";

        private readonly RectTransform itemsContainer;
        private readonly Transform containersRoot;
        private readonly Dictionary<Type, (object Pool, Action<object> ReleaseAction)> poolRegistry = new ();

        public T Get<T>() where T : ChatConversationsToolbarViewItem
        {
            if (poolRegistry.TryGetValue(typeof(T), out (object Pool, Action<object> ReleaseAction) poolHandle))
                return ((GameObjectPool<T>) poolHandle.Pool).Get();

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
            GameObjectPool<T> pool = new (containersRoot, () => GameObject.Instantiate(prefab), onGet: HandleGetObject);
            poolRegistry[typeof(T)] = (pool, obj => pool.Release((T)obj));
        }

        private void HandleGetObject<T>(T obj) where T : ChatConversationsToolbarViewItem
        {
            obj.transform.SetParent(itemsContainer);
            obj.transform.localScale = Vector3.one;
        }

        public ChatConversationsToolbarViewItemPool(
            RectTransform itemsContainer,
            ChatConversationsToolbarViewItem nearbyConversationItemPrefab,
            PrivateChatConversationsToolbarViewItem privateConversationItemPrefab,
            CommunityChatConversationsToolbarViewItem communityConversationItemPrefab)
        {
            this.itemsContainer = itemsContainer;
            containersRoot = GameObject.Find(ROOT_POOL_CONTAINER_NAME)!.transform;

            CreateObjectPool(nearbyConversationItemPrefab);
            CreateObjectPool(privateConversationItemPrefab);
            CreateObjectPool(communityConversationItemPrefab);
        }
    }
}
