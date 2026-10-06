using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Communities;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.TeleportPrompt;
using MVC;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    /// SEC-076: the link on an "event starting" notification is authored by whoever created the event through the
    /// open Events API, so clicking it must ask for consent before the client goes anywhere, and a crafted link must
    /// never throw out of the click dispatch.
    /// </summary>
    public class NotificationHandlerShould
    {
        private const string WORLD_NAME = "myworld.dcl.eth";
        private const string EVENT_URL = "https://decentraland.org/events/event/?id=00000000-0000-0000-0000-000000000000";

        private FakeMVCManager mvcManager = null!;
        private NotificationHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            NotificationsBusController.Reset();
            NotificationsBusController.Initialize(new NotificationsBusController());

            mvcManager = new FakeMVCManager();
            handler = new NotificationHandler(mvcManager);
        }

        [TearDown]
        public void TearDown()
        {
            handler.Dispose();
            NotificationsBusController.Reset();
        }

        [Test]
        public void AskForConsentBeforeChangingRealm()
        {
            // Arrange
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&realm={WORLD_NAME}&position=10,20");

            // Act
            Click(notification);

            // Assert
            Assert.That(mvcManager.IssuedCommands.Count, Is.EqualTo(1));

            ChangeRealmPromptController.Params prompt = RealmPromptAt(0);
            Assert.That(prompt.Realm, Is.EqualTo(WORLD_NAME));
            Assert.That(prompt.Position, Is.EqualTo(new Vector2Int(10, 20)));
        }

        [Test]
        public void AskForConsentBeforeTeleportingWithinGenesis()
        {
            // Arrange
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&position=-3,42");

            // Act
            Click(notification);

            // Assert
            Assert.That(mvcManager.IssuedCommands.Count, Is.EqualTo(1));

            var command = (ShowCommand<TeleportPromptView, TeleportPromptController.Params>)mvcManager.IssuedCommands[0];
            Assert.That(command.InputData.Coords, Is.EqualTo(new Vector2Int(-3, 42)));
        }

        [Test]
        public void AskForConsentWhenOnlyARealmIsGiven()
        {
            // Arrange
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&realm={WORLD_NAME}");

            // Act
            Click(notification);

            // Assert — no parcel, so the world's own spawn point decides where the player lands.
            Assert.That(mvcManager.IssuedCommands.Count, Is.EqualTo(1));
            Assert.That(RealmPromptAt(0).Position, Is.Null);
        }

        [Test]
        public void NormalizeTheWorldNameCasing()
        {
            // Arrange
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&realm=MyWorld.DCL.ETH");

            // Act
            Click(notification);

            // Assert
            Assert.That(RealmPromptAt(0).Realm, Is.EqualTo(WORLD_NAME));
        }

        [Test]
        public void NavigateNowhereWhileTheConsentPromptIsOpen()
        {
            // Arrange — ShowAsync stays pending: the prompt is on screen and the user has not answered.
            var promptOnScreen = new UniTaskCompletionSource();
            mvcManager.PendingShow = promptOnScreen;
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&realm={WORLD_NAME}&position=10,20");

            // Act
            Click(notification);

            // Assert — the consent prompt is the only thing dispatched.
            Assert.That(mvcManager.IssuedCommands.Count, Is.EqualTo(1));
            Assert.That(mvcManager.IssuedCommands[0], Is.InstanceOf<ShowCommand<ChangeRealmPromptView, ChangeRealmPromptController.Params>>());
        }

        [Test]
        public void NavigateNowhereWhenTheConsentPromptIsDeclined()
        {
            // Arrange
            var promptOnScreen = new UniTaskCompletionSource();
            mvcManager.PendingShow = promptOnScreen;
            EventStartedNotification notification = EventStartedWith($"{EVENT_URL}&realm={WORLD_NAME}&position=10,20");
            Click(notification);

            // Act — the prompt closes without the user approving.
            promptOnScreen.TrySetResult();

            // Assert — closing the prompt adds no navigation of its own; approving inside the prompt is the only
            // thing that moves the player.
            Assert.That(mvcManager.IssuedCommands.Count, Is.EqualTo(1));
            Assert.That(mvcManager.IssuedCommands[0], Is.InstanceOf<ShowCommand<ChangeRealmPromptView, ChangeRealmPromptController.Params>>());
        }

        [TestCase(null, TestName = "IgnoreACraftedLink(no link)")]
        [TestCase("", TestName = "IgnoreACraftedLink(empty)")]
        [TestCase("::not a uri::", TestName = "IgnoreACraftedLink(malformed uri)")]
        [TestCase("/events/event/?realm=myworld.dcl.eth", TestName = "IgnoreACraftedLink(relative uri)")]
        [TestCase(EVENT_URL + "&realm=myworld.dcl.eth&position=abc", TestName = "IgnoreACraftedLink(position=abc)")]
        [TestCase(EVENT_URL + "&realm=myworld.dcl.eth&position=5", TestName = "IgnoreACraftedLink(position without comma)")]
        [TestCase(EVENT_URL + "&realm=myworld.dcl.eth&position=1,2,3", TestName = "IgnoreACraftedLink(position with three parts)")]
        [TestCase(EVENT_URL + "&position=,", TestName = "IgnoreACraftedLink(empty parcel components)")]
        [TestCase(EVENT_URL + "&realm=https://evil.example.com&position=10,20", TestName = "IgnoreACraftedLink(realm is a url)")]
        [TestCase(EVENT_URL + "&realm=genesis&position=10,20", TestName = "IgnoreACraftedLink(realm is an alias)")]
        [TestCase(EVENT_URL + "&realm=evil.example.com&position=10,20", TestName = "IgnoreACraftedLink(realm is a host)")]
        [TestCase(EVENT_URL, TestName = "IgnoreACraftedLink(no destination)")]
        public void IgnoreACraftedLink(string? link)
        {
            // Act
            Click(EventStartedWith(link));

            // Assert
            Assert.That(mvcManager.IssuedCommands, Is.Empty);
        }

        [Test]
        public void NotBreakLaterSubscribersOnACraftedLink()
        {
            // Arrange — subscribers share one multicast delegate, so a throw here would also drop this listener.
            var laterSubscriberRan = false;

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(
                NotificationType.EVENTS_STARTED, _ => laterSubscriberRan = true);

            // Act
            Click(EventStartedWith("::not a uri::"));

            // Assert
            Assert.That(laterSubscriberRan, Is.True);
            Assert.That(mvcManager.IssuedCommands, Is.Empty);
        }

        [Test]
        public void IgnoreForeignNotificationPayloads()
        {
            // Act
            NotificationsBusController.Instance.ClickNotification(NotificationType.EVENTS_STARTED);
            NotificationsBusController.Instance.ClickNotification(NotificationType.EVENTS_STARTED, new NotificationBase());

            // Assert
            Assert.That(mvcManager.IssuedCommands, Is.Empty);
        }

        private ChangeRealmPromptController.Params RealmPromptAt(int index) =>
            ((ShowCommand<ChangeRealmPromptView, ChangeRealmPromptController.Params>)mvcManager.IssuedCommands[index]).InputData;

        private static EventStartedNotification EventStartedWith(string? link) =>
            new ()
            {
                Type = NotificationType.EVENTS_STARTED,
                Metadata = new EventStartedNotificationMetadata { Link = link! },
            };

        private static void Click(EventStartedNotification notification) =>
            NotificationsBusController.Instance.ClickNotification(NotificationType.EVENTS_STARTED, notification);

        private class FakeMVCManager : IMVCManager
        {
            private readonly List<object> issuedCommands = new ();

            public IReadOnlyList<object> IssuedCommands => issuedCommands;

            /// <summary>When set, <see cref="ShowAsync{TView,TInputData}" /> stays pending, modelling a prompt that is still on screen.</summary>
            public UniTaskCompletionSource? PendingShow { get; set; }

            public event Action<IController> OnViewShowed { add { } remove { } }
            public event Action<IController> OnViewClosed { add { } remove { } }

            public UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView
            {
                issuedCommands.Add(command);
                return PendingShow?.Task ?? UniTask.CompletedTask;
            }

            public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: IView { }

            public void SetAllViewsCanvasActive(bool isActive) { }

            public void SetAllViewsCanvasActive(IController except, bool isActive) { }

            public void CloseAllNonPersistentViews(CancellationToken ct = default) { }

            public bool IsAnyModalViewShowing() =>
                false;

            public void Dispose() { }
        }
    }
}
