using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Debug;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Rendering;
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
            public readonly ISituationalReactionTrigger Trigger;
            public readonly ISituationalReactionSimulation Simulation;
            public readonly SituationalReactionFacade Facade;
            public readonly ChatMessageReactionService MessageReactionService;
            public readonly ChatReactionDebugState DebugState;
            public readonly SituationalReactionDebugController DebugController;
            public readonly StreamReactionsEmitter StreamEmitter;

            internal Result(
                ISituationalReactionTrigger trigger,
                ISituationalReactionSimulation simulation,
                SituationalReactionFacade facade,
                ChatMessageReactionService messageReactionService,
                ChatReactionDebugState debugState,
                SituationalReactionDebugController debugController,
                StreamReactionsEmitter streamEmitter)
            {
                Trigger = trigger;
                Simulation = simulation;
                Facade = facade;
                MessageReactionService = messageReactionService;
                DebugState = debugState;
                DebugController = debugController;
                StreamEmitter = streamEmitter;
            }
        }

        public static Result Create(
            ChatReactionsConfig reactionsConfig,
            RectTransform uiLaneRect,
            IAvatarReactionPosition avatarPosition,
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache web3IdentityCache,
            IChatHistory chatHistory,
            DecentralandEnvironment environment,
            ChatSettingsAsset chatSettingsAsset,
            EventSubscriptionScope scope)
        {
            // --- Message bus ---
            IReactionMessageBus reactionBus = CreateReactionBus(
                messagePipesHub, userBlockingCacheProxy, web3IdentityCache, environment);
            scope.Add(reactionBus);

            // --- Message reactions ---
            var messageReactionService = new ChatMessageReactionService(reactionBus, chatHistory, web3IdentityCache);
            scope.Add(messageReactionService);

            // --- Simulations ---
            var uiSimulation = new ChatReactionUISimulation(reactionsConfig, uiLaneRect);
            var worldSimulation = new ChatReactionWorldSimulation(reactionsConfig, avatarPosition);
            scope.Add(uiSimulation);
            scope.Add(worldSimulation);

            // --- Split service classes ---
            var worldReactor = new LocalPlayerWorldReactor(worldSimulation, reactionsConfig.WorldLane, avatarPosition);

            var facade = new SituationalReactionFacade(reactionsConfig, uiSimulation, worldReactor, reactionBus);
            scope.Add(facade);

            var streamEmitter = new StreamReactionsEmitter(facade, reactionsConfig);
            scope.Add(streamEmitter);

            var remoteTarget = new SituationalRemoteTarget(
                () => reactionsConfig.MessageReactions.ReceiveStaggerInterval,
                () => reactionsConfig.MaxReceiveQueueDepth,
                () => reactionsConfig.DynamicStaggerRampStart,
                () => reactionsConfig.MinStaggerInterval,
                () => reactionsConfig.MaxRemoteUIReactionsPerSec,
                worldReactor, uiSimulation);

            var simulationLoop = new SituationalSimulationLoop(
                uiSimulation, worldSimulation, facade.NetworkBroadcaster, remoteTarget, worldReactor,
                facade, streamEmitter);

            // --- Settings bindings ---
            simulationLoop.WorldReactionsEnabled =
                chatSettingsAsset.chatBubblesVisibilitySettings != ChatBubbleVisibilitySettings.NONE;

            simulationLoop.ShowRemoteUIReactions = chatSettingsAsset.chatReactionsEnabled;

            ChatSettingsAsset.ChatBubblesVisibilityDelegate bubblesHandler =
                visibility => simulationLoop.WorldReactionsEnabled = visibility != ChatBubbleVisibilitySettings.NONE;
            chatSettingsAsset.BubblesVisibilityChanged += bubblesHandler;

            ChatSettingsAsset.ChatReactionsEnabledDelegate reactionsHandler =
                enabled => simulationLoop.ShowRemoteUIReactions = enabled;
            chatSettingsAsset.ChatReactionsEnabledChanged += reactionsHandler;

            scope.Add(new SettingsUnsubscriber(chatSettingsAsset, bubblesHandler, reactionsHandler));

            // --- Routing ---
            var reactionRouter = new ReactionRouter(reactionBus, remoteTarget, messageReactionService);
            scope.Add(reactionRouter);

            // --- Debug ---
            var debugState = new ChatReactionDebugState();
            scope.Add(debugState);

            var debugController = new SituationalReactionDebugController(uiSimulation, worldSimulation, avatarPosition);
            scope.Add(debugController);

            worldSimulation.SetDebugNearbyUICallback((emoji, count) => uiSimulation.TriggerUIReaction(emoji, count));

#if UNITY_EDITOR
            var reactionEventBus = new ChatReactionEventBus();
            scope.Add(reactionEventBus);

            facade.ReactionSent += (emoji, count, ts) =>
                reactionEventBus.NotifySent(new ReactionSentEvent(emoji, count, ts, ReactionType.Situational));

            remoteTarget.RemoteReactionProcessed += args =>
                reactionEventBus.NotifyReceived(new ReactionReceivedEvent(
                    args.WalletId, args.EmojiIndex, args.Count, args.Type, args.MessageId, args.IsRemoval));

            facade.NetworkFlushed += (unique, total, ts) =>
                reactionEventBus.NotifyFlushed(new ReactionFlushedEvent(unique, total, ts));

            var debugGo = new GameObject("[Debug] ChatReactions");
            var debugView = debugGo.AddComponent<ChatReactionDebugView>();
            debugView.Init(reactionsConfig, reactionEventBus);
            scope.Add(debugView);
#endif

            return new Result(facade, simulationLoop, facade, messageReactionService, debugState, debugController, streamEmitter);
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

            public SettingsUnsubscriber(
                ChatSettingsAsset chatSettingsAsset,
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
