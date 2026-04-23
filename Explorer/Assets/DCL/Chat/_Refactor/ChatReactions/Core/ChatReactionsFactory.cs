using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Debug;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Web3.Identities;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Composition root for the entire chat reactions subsystem.
    /// Creates all internal services, wires events and settings, and adds everything
    /// to the provided scope for lifecycle management.
    /// </summary>
    public static class ChatReactionsFactory
    {
        public readonly struct Result
        {
            public readonly SituationalReactionFacade Facade;
            public readonly ISituationalReactionSimulation Simulation;
            public readonly ChatMessageReactionService MessageReactionService;
            public readonly ChatReactionDebugState DebugState;
            public readonly SituationalReactionDebugController DebugController;

            internal Result(
                SituationalReactionFacade facade,
                ISituationalReactionSimulation simulation,
                ChatMessageReactionService messageReactionService,
                ChatReactionDebugState debugState,
                SituationalReactionDebugController debugController)
            {
                Facade = facade;
                Simulation = simulation;
                MessageReactionService = messageReactionService;
                DebugState = debugState;
                DebugController = debugController;
            }
        }

        public static Result Create(
            ChatReactionsConfig reactionsConfig,
            RectTransform uiLaneRect,
            Canvas uiLaneCanvas,
            IAvatarReactionPosition avatarPosition,
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache web3IdentityCache,
            IChatHistory chatHistory,
            DecentralandEnvironment environment,
            ChatSettingsAsset chatSettingsAsset,
            EventSubscriptionScope scope)
        {
            IReactionMessageBus reactionBus = CreateReactionBus(messagePipesHub,
                userBlockingCacheProxy,
                web3IdentityCache,
                environment);
            
            var messageReactionService = new ChatMessageReactionService(reactionBus,
                chatHistory,
                web3IdentityCache,
                reactionsConfig.MessageReactions.MaxDistinctReactionsPerMessage);
            
            var uiSimulation = new ChatReactionUISimulation(reactionsConfig, uiLaneRect, uiLaneCanvas);
            var worldSimulation = new ChatReactionWorldSimulation(reactionsConfig, avatarPosition);
            
            var worldReactor = new LocalPlayerWorldReactor(worldSimulation,
                reactionsConfig.WorldLane,
                avatarPosition);

            var situationalReactionFacade = new SituationalReactionFacade(reactionsConfig,
                uiSimulation,
                worldReactor,
                reactionBus);
            
            var streamEmitter = new StreamReactionsEmitter(situationalReactionFacade, reactionsConfig);
            
            var remoteTarget = new SituationalRemoteTarget(reactionsConfig,
                worldReactor,
                uiSimulation);

            var simulationLoop = new SituationalSimulationLoop(uiSimulation,
                worldSimulation,
                remoteTarget, worldReactor, situationalReactionFacade,
                streamEmitter);

            simulationLoop.WorldReactionsEnabled = chatSettingsAsset.chatBubblesVisibilitySettings != ChatBubbleVisibilitySettings.NONE;
            simulationLoop.UIReactionsEnabled = chatSettingsAsset.chatReactionsEnabled;

            ChatSettingsAsset.ChatBubblesVisibilityDelegate bubblesHandler = visibility => simulationLoop.WorldReactionsEnabled = visibility != ChatBubbleVisibilitySettings.NONE;
            chatSettingsAsset.BubblesVisibilityChanged += bubblesHandler;

            ChatSettingsAsset.ChatReactionsEnabledDelegate reactionsHandler = enabled => simulationLoop.UIReactionsEnabled = enabled;
            chatSettingsAsset.ChatReactionsEnabledChanged += reactionsHandler;

            var reactionRouter = new ReactionRouter(reactionBus, remoteTarget, messageReactionService);
            
            var debugState = new ChatReactionDebugState();
            var debugController = new SituationalReactionDebugController(uiSimulation, worldSimulation, avatarPosition);
            worldSimulation.SetDebugNearbyUICallback((emoji, count) => uiSimulation.TriggerUIReaction(emoji, count));

            scope.Add(uiSimulation);
            scope.Add(reactionBus);
            scope.Add(worldSimulation);
            scope.Add(messageReactionService);
            scope.Add(situationalReactionFacade);
            scope.Add(streamEmitter);
            scope.Add(new SettingsUnsubscriber(chatSettingsAsset, bubblesHandler, reactionsHandler));
            scope.Add(reactionRouter);
            scope.Add(debugState);
            scope.Add(debugController);
            
#if UNITY_EDITOR
            var reactionEventBus = new ChatReactionEventBus();
            scope.Add(reactionEventBus);

            situationalReactionFacade.ReactionSent += (emoji, count, ts) =>
                reactionEventBus.NotifySent(new ReactionSentEvent(emoji, count, ts, ReactionType.Situational));

            remoteTarget.RemoteReactionProcessed += args =>
                reactionEventBus.NotifyReceived(new ReactionReceivedEvent(
                    args.WalletId, args.EmojiIndex, args.Count, args.Type, args.MessageId, args.IsRemoval));

            situationalReactionFacade.NetworkFlushed += (unique, total, ts) =>
                reactionEventBus.NotifyFlushed(new ReactionFlushedEvent(unique, total, ts));

            var debugGo = new GameObject("[Debug] ChatReactions");
            var debugView = debugGo.AddComponent<ChatReactionDebugView>();
            debugView.Init(reactionsConfig, reactionEventBus);
            scope.Add(debugView);
#endif

            return new Result(situationalReactionFacade, simulationLoop, messageReactionService, debugState, debugController);
        }

        private static IReactionMessageBus CreateReactionBus(
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache web3IdentityCache,
            DecentralandEnvironment environment)
        {
            string serverEnv = environment switch
            {
                DecentralandEnvironment.Org => "prd",
                DecentralandEnvironment.Today => "prd",
                DecentralandEnvironment.Zone => "dev",
                _ => "local",
            };

            string routingUser = $"message-router-{serverEnv}-0";

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[ChatPlugin] Using MultiplayerReactionMessageBus (routingUser={routingUser})");
            return new MultiplayerReactionMessageBus(messagePipesHub, userBlockingCacheProxy, web3IdentityCache, routingUser);
        }

        private sealed class SettingsUnsubscriber : IDisposable
        {
            private readonly ChatSettingsAsset chatSettingsAsset;
            private readonly ChatSettingsAsset.ChatBubblesVisibilityDelegate bubblesHandler;
            private readonly ChatSettingsAsset.ChatReactionsEnabledDelegate reactionsHandler;

            public SettingsUnsubscriber(ChatSettingsAsset chatSettingsAsset,
                ChatSettingsAsset.ChatBubblesVisibilityDelegate bubblesHandler,
                ChatSettingsAsset.ChatReactionsEnabledDelegate reactionsHandler)
            {
                this.chatSettingsAsset = chatSettingsAsset;
                this.bubblesHandler = bubblesHandler;
                this.reactionsHandler = reactionsHandler;
            }

            public void Dispose()
            {
                chatSettingsAsset.BubblesVisibilityChanged -= bubblesHandler;
                chatSettingsAsset.ChatReactionsEnabledChanged -= reactionsHandler;
            }
        }
    }
}
