using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ChatReactionsFactoryShould
    {
        private ChatReactionsConfig config = null!;
        private ChatReactionsMessageConfig messageConfig = null!;
        private IMessagePipesHub pipesHub = null!;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            config = ScriptableObject.CreateInstance<ChatReactionsConfig>();
            messageConfig = ScriptableObject.CreateInstance<ChatReactionsMessageConfig>();
            config.MessageReactions = messageConfig;

            pipesHub = Substitute.For<IMessagePipesHub>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(messageConfig);
            Object.DestroyImmediate(config);

            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void BuildTheNetworkingOnABusThatSubscribesToNoPipeWhileReactionsAreDisabled()
        {
            // Act — the flag is off: a client that cannot show reactions must not decode them either
            (IReactionMessageBus reactionBus, ChatMessageReactionService messageReactionService) = ChatReactionsFactory.CreateNetworking(
                config,
                pipesHub,
                Substitute.For<IUserBlockingCache>(),
                new IWeb3IdentityCache.Fake(new IWeb3Identity.Random()),
                new ChatHistory(),
                DecentralandEnvironment.Zone);

            // Assert
            Assert.That(reactionBus, Is.InstanceOf<NullReactionMessageBus>());
            Assert.That(messageReactionService, Is.Not.Null);
            pipesHub.DidNotReceive().IslandPipe();
            pipesHub.DidNotReceive().ScenePipe();
            pipesHub.DidNotReceive().ChatPipe();

            messageReactionService.Dispose();
            reactionBus.Dispose();
        }
    }
}
