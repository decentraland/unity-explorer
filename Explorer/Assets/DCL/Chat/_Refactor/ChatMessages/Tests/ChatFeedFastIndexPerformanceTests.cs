using DCL.Chat.ChatServices;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Translation;
using DCL.Utilities;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace DCL.Chat.ChatMessages.Tests
{
    /// <summary>
    /// O(1)-lookup check for ChatMessageFeedPresenter.viewModelsMap.
    ///
    /// With auto-translate OFF the previous code nulled the fast index, degrading every
    /// FindViewModelById (reactions + translation lifecycle) to an O(N) linear scan. This asserts the
    /// index is built regardless of the auto-translate setting.
    ///
    /// Pure logic: the presenter is allocated without its (20-arg) constructor and the private
    /// method is exercised directly, so no Unity runtime / prefab is required.
    /// </summary>
    [TestFixture]
    public class ChatFeedFastIndexPerformanceTests
    {
        private sealed class FakeTranslationSettings : ITranslationSettings
        {
            public bool IsGloballyEnabled => false;
            public LanguageCode PreferredLanguage => default;
            public float TranslationTimeoutSeconds => 0f;
            public int MaxRetries => 0;
            public event Action<string> OnAutoTranslationSettingsChanged { add { } remove { } }

            // Auto-translate OFF for every conversation — the case that used to disable the index.
            public bool GetAutoTranslateForConversation(string conversationId) => false;
            public void SetAutoTranslateForConversation(string conversationId, bool isEnabled) { }
            public bool IsTranslationFeatureActive() => false;
        }

        private static FieldInfo Field(string name) =>
            typeof(ChatMessageFeedPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(ChatMessageFeedPresenter), name);

        private static readonly FieldInfo MESSAGE_ID_FIELD =
            typeof(ChatMessage).GetField(nameof(ChatMessage.MessageId))
            ?? throw new MissingFieldException(nameof(ChatMessage), nameof(ChatMessage.MessageId));

        private static ChatMessageViewModel MakeViewModel(string id)
        {
            // Build the message by setting MessageId on a boxed struct — avoids the ChatMessage
            // constructor (which touches game singletons) so this stays pure logic.
            object boxed = default(ChatMessage);
            MESSAGE_ID_FIELD.SetValue(boxed, id);

            ChatMessageViewModel vm = ChatMessageViewModel.POOL.Get();
            vm.Message = (ChatMessage)boxed;
            return vm;
        }

        // The fast index must be maintained even with auto-translate OFF.
        [Test]
        public void RebuildFastIndex_AutoTranslateOff_BuildsMap()
        {
            const int N = 500;

            var presenter = (ChatMessageFeedPresenter)FormatterServices.GetUninitializedObject(typeof(ChatMessageFeedPresenter));

            var viewModels = new List<ChatMessageViewModel>(N);
            for (int i = 0; i < N; i++)
                viewModels.Add(MakeViewModel($"id_{i}"));

            try
            {
                Field("currentChannelService").SetValue(presenter, new CurrentChannelService());
                Field("translationSettings").SetValue(presenter, new FakeTranslationSettings());
                Field("viewModels").SetValue(presenter, viewModels);
                // Seed a real dictionary so RebuildFastIndexIfNeeded's Clear() has something to clear.
                Field("viewModelsMap").SetValue(presenter, new Dictionary<string, ChatMessageViewModel>());

                typeof(ChatMessageFeedPresenter)
                    .GetMethod("RebuildFastIndexIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(presenter, null);

                object? map = Field("viewModelsMap").GetValue(presenter);

                Assert.IsNotNull(map,
                    "viewModelsMap must be maintained even when auto-translate is OFF; " +
                    "a null map means FindViewModelById degrades to an O(N) scan");
                Assert.AreEqual(N, ((IDictionary)map!).Count,
                    "every non-separator message must have an O(1) index entry");
            }
            finally
            {
                foreach (ChatMessageViewModel vm in viewModels)
                    ChatMessageViewModel.POOL.Release(vm);
            }
        }
    }
}
