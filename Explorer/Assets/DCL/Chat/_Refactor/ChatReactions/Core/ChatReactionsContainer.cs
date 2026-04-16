using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Debug;
using DCL.Chat.ChatReactions.Simulation.World;
using DCL.Chat.History;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Web3.Identities;
using UnityEngine;
using DCL.Multiplayer.Connections.DecentralandUrls;
using Utility;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    ///     A small scoped container for the chat reactions system to avoid God Object issues and eliminate ObjectProxy usage.
    ///     Created in DynamicWorldContainer; initialized by ChatPlugin when runtime dependencies are available.
    /// </summary>
    public sealed class ChatReactionsContainer : IDisposable
    {
        private readonly EventSubscriptionScope scope = new ();

        public ChatReactionsFactory.Result? Result { get; private set; }
        public ChatReactionsConfig? Config { get; private set; }
        public bool IsInitialized => Result != null;

        public void Initialize(
            ChatReactionsConfig config,
            RectTransform uiLaneRect,
            IAvatarReactionPosition avatarPosition,
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache web3IdentityCache,
            IChatHistory chatHistory,
            DecentralandEnvironment environment,
            ChatSettingsAsset chatSettingsAsset)
        {
            Config = config;

            Result = ChatReactionsFactory.Create(
                config,
                uiLaneRect,
                avatarPosition,
                messagePipesHub,
                userBlockingCacheProxy,
                web3IdentityCache,
                chatHistory,
                environment,
                chatSettingsAsset,
                scope);
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
