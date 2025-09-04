using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Translation.Service;
using UnityEngine;

namespace DCL.Chat.ChatServices.ChatTranslationService.Tests
{
    public class TranslationTester : MonoBehaviour
    {
        private ITranslationService translationService;
        private int messageCounter = 0;
        
        public void Initialize(ITranslationService translationService)
        {
            this.translationService = translationService;
        }
        
        private ChatMessage CreateTestMessage(string body)
        {
            messageCounter++;
            string fakeMessageId = $"test-message-{messageCounter}";

            // This is where you would normally get the data from LiveKit
            return new ChatMessage(
                fakeMessageId,
                body,
                "TestUser",
                "0x12345",
                false,
                "TestUserWalletId",
                System.DateTime.UtcNow.ToOADate()
            );
        }

        [ContextMenu("1. Test: Translate a Normal Message")]
        private void TestNormalTranslation()
        {
            var message = CreateTestMessage("Hello world, this is a test of the translation system.");
            ReportHub.Log(ReportCategory.UNSPECIFIED,$"[TestHarness] Sending message '{message.MessageId}' for translation.");
            translationService.ProcessIncomingMessage(message);
            // Now, watch your console for the events (Requested, Translated/Failed)
        }

        [ContextMenu("2. Test: Translate a Message that Should Fail")]
        private void TestFailingTranslation()
        {
            // The MockProvider is set to fail every 5th request.
            // Run the normal test a few times first to increment the counter.
            var message = CreateTestMessage("This message is designed to trigger a simulated failure.");
            ReportHub.Log(ReportCategory.UNSPECIFIED,$"[TestHarness] Sending message '{message.MessageId}' that should fail.");
            translationService.ProcessIncomingMessage(message);
        }

        [ContextMenu("3. Test: A Trivial Message (Should be Skipped by Policy)")]
        private void TestTrivialMessage()
        {
            var message = CreateTestMessage("hi");
            ReportHub.Log(ReportCategory.UNSPECIFIED,$"[TestHarness] Sending trivial message '{message.MessageId}'. The policy should skip this. No 'Requested' event should fire.");
            translationService.ProcessIncomingMessage(message);
        }

        // [ContextMenu("4. Test: Manual Translation Trigger")]
        // private void TestManualTranslation()
        // {
        //     var message = CreateTestMessage("This message requires manual translation.");
        //     // We need to add it to the memory first to simulate it existing in the chat
        //     var translationMemory = FindObjectOfType<InMemoryTranslationMemory>(); // Quick and dirty for testing
        //     if (translationMemory != null)
        //     {
        //         var newTranslation = new Models.MessageTranslation(message.Message, Models.LanguageCode.Es);
        //         translationMemory.Set(message.MessageId, newTranslation);
        //
        //         ReportHub.Log(ReportCategory.UNSPECIFIED,$"[TestHarness] Manually translating message '{message.MessageId}'.");
        //         translationService.TranslateManualAsync(message.MessageId, this.GetCancellationTokenOnDestroy()).Forget();
        //     }
        //     else
        //     {
        //         ReportHub.LogError(ReportCategory.UNSPECIFIED,"Could not find InMemoryTranslationMemory in the scene to run this test.");
        //     }
        // }
    }
}