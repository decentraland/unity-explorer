using DCL.Chat.History;
using DCL.ChatArea;
using DCL.Web3.Identities;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;

namespace DCL.Chat.ChatServices.Tests
{
    [TestFixture]
    public class ChatOpenerShould
    {
        private ChatEventBus chatEventBus = null!;
        private IMVCManager mvcManager = null!;
        private ChatOpener chatOpener = null!;
        private IDisposable focusSubscription = null!;
        private int focusRequests;

        [SetUp]
        public void SetUp()
        {
            chatEventBus = new ChatEventBus();
            mvcManager = Substitute.For<IMVCManager>();
            chatOpener = new ChatOpener(chatEventBus, mvcManager, Substitute.For<IWeb3IdentityCache>(), Substitute.For<IChatHistory>());
            focusRequests = 0;
            focusSubscription = chatEventBus.Subscribe<ChatEvents.FocusRequestedEvent>(_ => focusRequests++);
        }

        [TearDown]
        public void TearDown() =>
            focusSubscription.Dispose();

        [Test]
        public void DoNothingWhileTheChatIsNotOnScreen()
        {
            // Arrange
            mvcManager.IsShowing<ChatMainSharedAreaView, ControllerNoData>().Returns(false);

            // Act
            bool focused = chatOpener.CloseAllViewsAndFocusChat();

            // Assert
            Assert.That(focused, Is.False);
            mvcManager.DidNotReceive().CloseAllNonPersistentViews(Arg.Any<CancellationToken>());
            Assert.That(focusRequests, Is.Zero);
        }

        [Test]
        public void CloseTheViewsAndFocusTheChatOnScreen()
        {
            // Arrange
            mvcManager.IsShowing<ChatMainSharedAreaView, ControllerNoData>().Returns(true);

            // Act
            bool focused = chatOpener.CloseAllViewsAndFocusChat();

            // Assert
            Assert.That(focused, Is.True);
            mvcManager.Received(1).CloseAllNonPersistentViews(Arg.Any<CancellationToken>());
            Assert.That(focusRequests, Is.EqualTo(1));
        }
    }
}
