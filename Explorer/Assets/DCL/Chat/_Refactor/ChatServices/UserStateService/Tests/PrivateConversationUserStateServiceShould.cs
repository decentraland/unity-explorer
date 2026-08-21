using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Settings.Settings;
using DCL.SocialService;
using DCL.VoiceChat;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Chat.ChatServices.Tests
{
    [TestFixture]
    public class PrivateConversationUserStateServiceShould
    {
        private IUserBlockingCache userBlockingCache = null!;
        private ChatSettingsAsset settingsAsset = null!;
        private PrivateConversationUserStateService service = null!;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            userBlockingCache = Substitute.For<IUserBlockingCache>();
            settingsAsset = ScriptableObject.CreateInstance<ChatSettingsAsset>();

            IRoomHub roomHub = Substitute.For<IRoomHub>();
            roomHub.ChatRoom().Returns(NullRoom.INSTANCE);

            // friendsService: null mirrors sessions where the Friends feature is force-disabled (local scene development)
            service = new PrivateConversationUserStateService(
                new CurrentChannelService(),
                new ChatEventBus(),
                userBlockingCache,
                friendsService: null,
                settingsAsset,
                new RPCChatPrivacyService(Substitute.For<IRPCSocialServices>(), settingsAsset),
                Substitute.For<IFriendsEventBus>(),
                roomHub);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settingsAsset);
        }

        [Test]
        public void ResolveUserStateWithoutFriendsService()
        {
            LogAssert.ignoreFailingMessages = true;

            // The path has no pending awaits when friendsService is null, so the task completes synchronously
            UniTask<PrivateConversationUserStateService.UserState> task = service.GetChatUserStateAsync("0xabc", CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            PrivateConversationUserStateService.UserState state = task.GetAwaiter().GetResult();

            Assert.That(state.IsConsideredOnline, Is.False);
            Assert.That(state.ChatUserState, Is.EqualTo(PrivateConversationUserStateService.ChatUserState.Disconnected));
        }

        [Test]
        public void ReportCallStatusOfflineWhenResolutionFails()
        {
            LogAssert.ignoreFailingMessages = true;

            userBlockingCache.UserIsBlocked(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("blocking cache unavailable"));

            var command = new GetUserCallStatusCommand(service);

            UniTask<CallButtonPresenter.OtherUserCallStatus> task = command.ExecuteAsync("0xabc", CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(CallButtonPresenter.OtherUserCallStatus.UserOffline));
        }
    }
}
