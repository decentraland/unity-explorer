using DCL.Chat;
using DCL.Chat.History;
using DCL.FeatureFlags;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies CalculatePreferredWidth routes text through the guarded SetMessageContent instead of a raw SetText,
    /// so re-binding an already-set string leaves the TMP text clean (havePropertiesChanged stays false) while a
    /// genuinely different string still recomputes the width correctly.
    /// </summary>
    [Category("Performance")]
    public class ChatBubblePreferredWidthPerformanceTest
    {
#if UNITY_EDITOR
        private const string PREFAB_PATH = "Assets/DCL/Chat/Assets/ChatEntries/ChatEntry_OtherUser.prefab";

        private Canvas canvas = null!;
        private ChatEntryMessageBubbleElement bubble = null!;

        private static readonly (string text, string name)[] FIXTURE =
        {
            ("hello world", "Alice"),
            ("a message with <link=\"x\">a link</link> inside", "Bob"),
            ("colored <color=#ff0000>text</color> here", "Carol"),
            ("plain short", "AVeryLongDisplayNameLongerThanTheMessage"),
            ("emoji tag \\U0001F600 present", "Dave"),
        };

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>(),
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            }));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load chat entry prefab from {PREFAB_PATH}");

            var canvasGo = new GameObject("test-canvas", typeof(Canvas));
            canvas = canvasGo.GetComponent<Canvas>();

            GameObject instance = Object.Instantiate(prefab, canvas.transform);
            bubble = instance.GetComponentInChildren<ChatEntryMessageBubbleElement>(true);
            Assert.IsNotNull(bubble, "ChatEntryMessageBubbleElement not found under prefab");
        }

        [TearDown]
        public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
            FeatureFlagsConfiguration.Reset();
        }

        private static ChatMessage Msg(string text, string name) =>
            new (text, name, "0x1111111111111111111111111111111111111111", false, "#1234", 0.0);

        [Test]
        [Performance]
        public void CalculatePreferredWidth_NoRedundantTmpReparse()
        {
            foreach ((string text, string name) in FIXTURE)
            {
                ChatMessage msg = Msg(text, name);

                bubble.messageContentElement.SetMessageContent(text);
                float w1 = bubble.CalculatePreferredWidth(text, msg);

                Assert.IsFalse(bubble.messageContentElement.messageContentText.havePropertiesChanged,
                    $"CalculatePreferredWidth dirtied the TMP text for '{text}' — the redundant reparse is still present");

                float w2 = bubble.CalculatePreferredWidth(text, msg);
                Assert.AreEqual(w1, w2, "CalculatePreferredWidth is not idempotent for an unchanged bind");
            }

            ChatMessage msgA = Msg("first text A", "Alice");
            ChatMessage msgB = Msg("second, longer text B here", "Alice");

            bubble.messageContentElement.SetMessageContent(msgA.Message);
            bubble.CalculatePreferredWidth(msgA.Message, msgA);
            float switched = bubble.CalculatePreferredWidth(msgB.Message, msgB);

            bubble.messageContentElement.SetMessageContent(msgB.Message);
            float fresh = bubble.CalculatePreferredWidth(msgB.Message, msgB);

            Assert.AreEqual(fresh, switched, "different-text width diverged from a fresh bind — the guard swallowed a real change");

            Measure.Method(() =>
                    {
                        foreach ((string text, string name) in FIXTURE)
                            bubble.SetMessageData(Msg(text, name));
                    })
                   .WarmupCount(5).MeasurementCount(15).GC().Run();
        }
#endif
    }
}
