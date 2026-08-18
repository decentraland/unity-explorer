using DCL.Backpack.Gifting;
using DCL.Notifications.NotificationEntry;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Backpack.Gifting.Tests.UnitTests
{
    /// <summary>
    /// Regression pin for the received-gift copy fix (bug: "gift notifications arrive before the
    /// gift does"). Backpack visibility can lag the transfer-received notification by minutes (a
    /// server-side ownership cache the client cannot bust), so every gift-received surface must
    /// say the item is still in transit instead of implying it is already sitting in the backpack.
    /// This guards against silently reverting to the old immediate-availability copy.
    /// </summary>
    [TestFixture]
    public class GiftReceivedCopyShould
    {
        private const string OLD_NOTIFICATION_TITLE = "sent you a gift!";
        private const string OLD_GIFT_OPENED_TITLE = "ITEM OPENED";

        private const string ON_ITS_WAY_SENTENCE = "It's on its way to your Backpack.";

        private static readonly FieldInfo? TITLE_TEXT_FIELD =
            typeof(GiftToastView).GetField("<TitleText>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<GameObject> spawned = new (2);

        [SetUp]
        public void SetUp()
        {
            // GiftReceivedNotification's constructor reaches into the NotificationsBus singleton
            // (it subscribes for click routing), so it must be live before any test constructs
            // one - otherwise construction throws ArgumentNullException.
            NotificationsBusController.Initialize(new NotificationsBusController());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);

            spawned.Clear();

            NotificationsBusController.Reset();
        }

        [Test]
        public void StateGiftIsOnItsWayInTheNotificationsPanelEntryTitle()
        {
            string title = new GiftReceivedNotification().GetTitle();

            Assert.That(title, Does.Contain(ON_ITS_WAY_SENTENCE),
                "GiftReceivedNotification.GetTitle() must tell the recipient the gift is still on its way, not just that it was sent.");
            Assert.That(title, Is.Not.EqualTo(OLD_NOTIFICATION_TITLE),
                "GiftReceivedNotification.GetTitle() regressed to the old copy, which implied the gift is already in the backpack.");
        }

        [Test]
        public void StateGiftOpenedPopupSubtitleIsOnItsWay()
        {
            const string newGiftOpenedTitle = "ON ITS WAY TO YOUR BACKPACK";

            Assert.That(GiftingTextIds.GiftOpenedTitle, Is.EqualTo(newGiftOpenedTitle),
                "GiftingTextIds.GiftOpenedTitle (the received-gift popup subtitle) must say the gift is on its way, not that it has already arrived.");
            Assert.That(GiftingTextIds.GiftOpenedTitle, Is.Not.EqualTo(OLD_GIFT_OPENED_TITLE),
                "GiftingTextIds.GiftOpenedTitle regressed to the old \"ITEM OPENED\" copy, which - combined with the popup's Open Backpack CTA - implied immediate availability.");
        }

        [Test]
        public void StateGiftIsOnItsWayInTheToast_AddressVariant()
        {
            GiftToastView view = CreateToastView();
            var notification = new GiftReceivedNotification
            {
                Metadata = new GiftReceivedNotificationMetadata { SenderAddress = "0x123456" },
            };

            view.Configure(notification);

            string title = view.TitleText.text;
            const string oldTitle = "0x123456 sent you something!";

            Assert.That(title, Does.Contain(ON_ITS_WAY_SENTENCE),
                "GiftToastView.Configure (short-address variant) must tell the recipient the gift is still on its way.");
            Assert.That(title, Is.Not.EqualTo(oldTitle),
                "GiftToastView.Configure (short-address variant) regressed to the old copy, which implied the gift already arrived.");
        }

        [Test]
        public void StateGiftIsOnItsWayInTheToast_ResolvedNameVariant()
        {
            GiftToastView view = CreateToastView();

            view.UpdateSenderName("Alice", Color.white);

            string title = view.TitleText.text;
            const string oldTitle = "<color=#FFFFFF><b>Alice</b></color> sent you something!";

            Assert.That(title, Does.Contain(ON_ITS_WAY_SENTENCE),
                "GiftToastView.UpdateSenderName (resolved-name variant) must tell the recipient the gift is still on its way.");
            Assert.That(title, Is.Not.EqualTo(oldTitle),
                "GiftToastView.UpdateSenderName (resolved-name variant) regressed to the old copy, which implied the gift already arrived.");
        }

        [Test]
        public void StateGiftIsOnItsWayInThePanelEntry_AddressVariant()
        {
            GiftNotificationView view = CreatePanelEntryView();

            var notification = new GiftReceivedNotification
            {
                Metadata = new GiftReceivedNotificationMetadata { SenderAddress = "0x123456" },
            };

            view.Configure(notification);

            string header = view.HeaderText.text;
            const string oldHeader = "0x123456 sent you a something!";

            Assert.That(header, Does.Contain(ON_ITS_WAY_SENTENCE),
                "GiftNotificationView.Configure (short-address variant) must tell the recipient the gift is still on its way.");
            Assert.That(header, Is.Not.EqualTo(oldHeader),
                "GiftNotificationView.Configure (short-address variant) regressed to the old copy, which implied the gift already arrived.");
        }

        [Test]
        public void StateGiftIsOnItsWayInThePanelEntry_ResolvedNameVariant()
        {
            GiftNotificationView view = CreatePanelEntryView();

            view.UpdateSenderName("Alice", Color.white);

            string header = view.HeaderText.text;
            const string oldHeader = "<color=#FFFFFF>Alice</color> sent you something!";

            Assert.That(header, Does.Contain(ON_ITS_WAY_SENTENCE),
                "GiftNotificationView.UpdateSenderName (resolved-name variant, GiftingTextIds.GiftReceivedTitleFormat) must tell the recipient the gift is still on its way.");
            Assert.That(header, Is.Not.EqualTo(oldHeader),
                "GiftNotificationView.UpdateSenderName (resolved-name variant) regressed to the old copy, which implied the gift already arrived.");
        }

        private GiftNotificationView CreatePanelEntryView()
        {
            var go = new GameObject(nameof(GiftNotificationView), typeof(GiftNotificationView), typeof(TextMeshProUGUI));
            spawned.Add(go);

            GiftNotificationView view = go.GetComponent<GiftNotificationView>();
            view.HeaderText = go.GetComponent<TextMeshProUGUI>();

            return view;
        }

        private GiftToastView CreateToastView()
        {
            Assert.IsNotNull(TITLE_TEXT_FIELD,
                "GiftToastView.TitleText backing field '<TitleText>k__BackingField' was not found via reflection - " +
                "the field/property was likely renamed; update this test's field name before trusting its result.");

            var go = new GameObject(nameof(GiftToastView), typeof(GiftToastView), typeof(TextMeshProUGUI));
            spawned.Add(go);

            GiftToastView view = go.GetComponent<GiftToastView>();
            TextMeshProUGUI titleText = go.GetComponent<TextMeshProUGUI>();

            TITLE_TEXT_FIELD!.SetValue(view, titleText);

            return view;
        }
    }
}
