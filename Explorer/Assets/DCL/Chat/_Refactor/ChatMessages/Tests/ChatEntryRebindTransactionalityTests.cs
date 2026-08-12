using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.FeatureFlags;
using NUnit.Framework;
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Chat.ChatMessages.Tests
{
    /// <summary>
    /// EditMode checks that the ChatEntryView rebind gate is transactional: the last-bound
    /// latch must only survive a bind that ran to completion. The harness wires just the username
    /// element and deliberately leaves the bubble element unassigned, so every full bind renders the
    /// username and then throws — the same shape as any mid-bind exception — without needing the
    /// prefab graph. The game singletons the ChatMessage constructor reaches are initialized in
    /// SetUp, mirroring the sibling ChatEntryRebindGatePerformanceTests.
    /// </summary>
    [TestFixture]
    public class ChatEntryRebindTransactionalityTests
    {
        private GameObject entryGo = null!;
        private GameObject usernameGo = null!;
        private ChatEntryView entry = null!;
        private TMP_Text userNameText = null!;
        private ChatMessageViewModel? vmA;
        private ChatMessageViewModel? vmB;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Reset();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            // Adding ChatEntryView in edit mode invokes its editor Reset(), which dereferences the
            // (deliberately) unwired bubble element; silence that expected log during creation only.
            LogAssert.ignoreFailingMessages = true;

            try
            {
                entryGo = new GameObject("entry");
                entry = entryGo.AddComponent<ChatEntryView>();
            }
            finally { LogAssert.ignoreFailingMessages = false; }

            usernameGo = new GameObject("username");
            ChatEntryUsernameElement usernameElement = usernameGo.AddComponent<ChatEntryUsernameElement>();

            userNameText = new GameObject("userName").AddComponent<TextMeshProUGUI>();
            TMP_Text walletIdText = new GameObject("walletId").AddComponent<TextMeshProUGUI>();
            userNameText.transform.SetParent(usernameGo.transform);
            walletIdText.transform.SetParent(usernameGo.transform);

            SetAutoProperty(usernameElement, "userName", userNameText);
            SetAutoProperty(usernameElement, "walletIdText", walletIdText);

            entry.usernameElement = usernameElement;
        }

        [TearDown]
        public void TearDown()
        {
            if (vmA != null) { ChatMessageViewModel.POOL.Release(vmA); vmA = null; }
            if (vmB != null) { ChatMessageViewModel.POOL.Release(vmB); vmB = null; }
            if (entryGo != null) UnityEngine.Object.DestroyImmediate(entryGo);
            if (usernameGo != null) UnityEngine.Object.DestroyImmediate(usernameGo);
        }

        // A bind that throws after rendering the username but before the end-of-method latch assignment
        // must leave the gate OPEN. Otherwise a later re-offer of the previously latched view model
        // (same reference, same Version — both genuinely unchanged) false-hits the gate, skips the
        // corrective SetUsername, and permanently strands the interrupted bind's username on the wrong row.
        [Test]
        public void SetItemData_ReofferAfterInterruptedBind_RebindsFully()
        {
            vmA = ChatMessageViewModel.POOL.Get();
            vmA!.Message = new ChatMessage("hi", "Alice", "0xaaaa", false, "#1111", 0d);
            vmB = ChatMessageViewModel.POOL.Get();
            vmB!.Message = new ChatMessage("yo", "Bob", "0xbbbb", false, "#2222", 0d);

            // Against this harness every full bind renders the username, then throws at the unwired
            // bubble element — after the visual write, before the latch assignment.
            Assert.Catch<NullReferenceException>(() => Bind(vmA!));
            Assert.AreEqual("Alice", userNameText.text, "the interrupted bind must still have rendered the username");

            // Latch the state a COMPLETED bind of vmA would have recorded.
            SetLatch(entry, vmA!, vmA!.Version);

            // An interrupted bind of a different view model paints its username over the cell...
            Assert.Catch<NullReferenceException>(() => Bind(vmB!));
            Assert.AreEqual("Bob", userNameText.text, "the interrupted bind must have painted the incoming username");

            // ...so re-offering the latched view model must perform a full corrective rebind: the
            // interrupted bind invalidated the latch, and the gate must not report "unchanged".
            Assert.Catch<NullReferenceException>(() => Bind(vmA!));
            Assert.AreEqual("Alice", userNameText.text,
                "a re-offer after an interrupted bind must fully rebind instead of being gated as unchanged");
        }

        private void Bind(ChatMessageViewModel viewModel) =>
            entry.SetItemData(viewModel, (id, view) => { }, null, () => false);

        private static void SetAutoProperty(object target, string propertyName, object value) =>
            (target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
             ?? throw new MissingFieldException(target.GetType().Name, propertyName))
            .SetValue(target, value);

        private static void SetLatch(ChatEntryView view, ChatMessageViewModel viewModel, int version)
        {
            SetPrivateField(view, "lastBoundViewModel", viewModel);
            SetPrivateField(view, "lastBoundVersion", version);
        }

        private static void SetPrivateField(object target, string name, object value) =>
            (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
             ?? throw new MissingFieldException(target.GetType().Name, name))
            .SetValue(target, value);
    }
}
