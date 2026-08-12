using DCL.Chat;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace DCL.Chat.ChatMessages.Tests
{
    /// <summary>
    /// Per-bind allocation check for ChatMessageFeedView.
    ///
    /// Instead of measuring GC bytes (noisy), this asserts DELEGATE IDENTITY: the forwarders the feed
    /// hands to a cell on every offer must be the SAME instance across re-offers. WireCachedHandlers
    /// assigns exactly ChatMessageFeedView.{Translate,Revert,Reaction}Forwarder to the cell, so those
    /// accessors ARE the per-bind allocation surface — asserting they return a cached instance across
    /// reads is equivalent to asserting a re-offer allocates no new delegate.
    ///
    /// Runs in plain EditMode: ChatMessageFeedView has no Awake/Reset, so AddComponent needs no wired
    /// serialized refs, and the forwarder accessors only read/materialize the cached delegate fields —
    /// no prefab, no TMP, no ChatEntryView MonoBehaviour (whose editor Reset() would NRE on a bare
    /// AddComponent because its serialized bubble element is unwired).
    /// </summary>
    [TestFixture]
    public class ChatFeedBindAllocPerformanceTests
    {
        private ChatMessageFeedView? feed;

        [SetUp]
        public void SetUp()
        {
            feed = new GameObject("feed").AddComponent<ChatMessageFeedView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (feed != null) { UnityEngine.Object.DestroyImmediate(feed.gameObject); feed = null; }
        }

        private static void SetPrivateField(object target, string name, object value) =>
            (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
             ?? throw new MissingFieldException(target.GetType().Name, name))
            .SetValue(target, value);

        // Re-offering a cell must reuse the cached translate/revert forwarders (zero new allocations).
        [Test]
        public void RebindWiring_ReusesCachedTranslateRevertDelegates()
        {
            Action<string>? translate1 = feed!.TranslateForwarder;
            Action<string>? revert1 = feed.RevertForwarder;

            Assert.IsNotNull(translate1, "translate forwarder must be materialized");
            Assert.IsNotNull(revert1, "revert forwarder must be materialized");

            Assert.AreSame(translate1, feed.TranslateForwarder,
                "translate forwarder must be the same cached instance across re-offers");
            Assert.AreSame(revert1, feed.RevertForwarder,
                "revert forwarder must be the same cached instance across re-offers");
        }

        // With reactions ENABLED the reaction forwarder must also be cached and reused (not
        // re-allocated per bind). reactionsEnabled is forced on via reflection so the assertion is
        // meaningful — a feed with reactions disabled would hand out null on both reads.
        [Test]
        public void RebindWiring_ReusesCachedReactionDelegate()
        {
            SetPrivateField(feed!, "reactionsEnabled", true);

            Action<string, ChatEntryView>? reaction1 = feed!.ReactionForwarder;

            Assert.IsNotNull(reaction1, "reaction forwarder must be materialized when reactions are enabled");

            Assert.AreSame(reaction1, feed.ReactionForwarder,
                "reaction forwarder must be the same cached instance across re-offers");
        }
    }
}
