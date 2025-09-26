/*
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices.TranslationService.Utilities;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Translation.Service;
using DCL.Translation.Service.Provider;
using DCL.Translation.Settings;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatServices.ChatTranslationService.Tests
{
    public class TranslationTester : MonoBehaviour
    {
        private const string TEST_CONVERSATION_ID = "test-channel";

        private ITranslationService translationService;
        private ITranslationProvider translationProvider;
        private ITranslationSettings translationSettings;
        private int messageCounter = 0;

        public void Initialize(ITranslationService translationService,
            ITranslationProvider translationProvider,
            ITranslationSettings translationSettings)
        {
            this.translationService = translationService;
            this.translationProvider = translationProvider;
            this.translationSettings = translationSettings;
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
                DateTime.UtcNow.ToOADate()
            );
        }

        [ContextMenu("1. Test: Translate a Normal Message")]
        private void TestNormalTranslation()
        {
            var message = CreateTestMessage("Hello world, this is a test of the translation system.");
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"[TestHarness] Sending message '{message.MessageId}' for translation.");
            translationService.ProcessIncomingMessage(message.MessageId, message.Message, TEST_CONVERSATION_ID);
            // Now, watch your console for the events (Requested, Translated/Failed)
        }

        [ContextMenu("2. Test: Translate a Message that Should Fail")]
        private void TestFailingTranslation()
        {
            // The MockProvider is set to fail every 5th request.
            // Run the normal test a few times first to increment the counter.
            var message = CreateTestMessage("This message is designed to trigger a simulated failure.");
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"[TestHarness] Sending message '{message.MessageId}' that should fail.");
            translationService.ProcessIncomingMessage(message.MessageId, message.Message, TEST_CONVERSATION_ID);
        }

        [ContextMenu("3. Test: A Trivial Message (Should be Skipped by Policy)")]
        private void TestTrivialMessage()
        {
            var message = CreateTestMessage("hi");
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"[TestHarness] Sending trivial message '{message.MessageId}'. The policy should skip this. No 'Requested' event should fire.");
            translationService.ProcessIncomingMessage(message.MessageId, message.Message, TEST_CONVERSATION_ID);
        }

        [ContextMenu("4. Test: Manual Translation Trigger")]
        private void TestManualTranslation()
        {
            // --- REWRITTEN AND UNCOMMENTED ---
            var message = CreateTestMessage("This message should only be translated manually.");
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"[TestHarness] Manually translating message '{message.MessageId}'. This should work even if auto-translate is OFF.");

            // We call the service directly with the data it needs.
            // The service itself will handle creating the record in the translation memory.
            translationService.TranslateManualAsync(message.MessageId, message.Message, this.GetCancellationTokenOnDestroy()).Forget();
        }

        [ContextMenu("5. Toggle Auto-Translate for Test Channel")]
        private void ToggleAutoTranslate()
        {
            if (translationSettings == null)
            {
                ReportHub.LogError(ReportCategory.UNSPECIFIED, "TranslationSettings not initialized in tester!");
                return;
            }

            bool currentStatus = translationSettings.GetAutoTranslateForConversation(TEST_CONVERSATION_ID);
            bool newStatus = !currentStatus;

            translationSettings.SetAutoTranslateForConversation(TEST_CONVERSATION_ID, newStatus);
            ReportHub.Log(ReportCategory.UNSPECIFIED, $"[TestHarness] Auto-Translate for channel '{TEST_CONVERSATION_ID}' set to: {newStatus}");
        }



    // [ContextMenu("Run Translation Test")]
        // public async void RunTranslationTest()
        // {
        //     ReportHub.Log(ReportCategory.CHAT_TRANSLATE,"Starting translation test...");
        //
        //     try
        //     {
        //         var result = await translationService.TranslateManualAsync("Hello world my friend!", LanguageCode.EN, LanguageCode.ES, CancellationToken.None);
        //         ReportHub.Log(ReportCategory.CHAT_TRANSLATE,$"SUCCESS! Translated text: '{result.TranslatedText}'. Detected source: {result.DetectedSourceLanguage}");
        //
        //         var result2 = await TranslateAsync("This is a test of the translation service.", LanguageCode.EN, LanguageCode.JA, CancellationToken.None);
        //         ReportHub.Log(ReportCategory.CHAT_TRANSLATE,$"SUCCESS! Translated text: '{result2.TranslatedText}'. Detected source: {result2.DetectedSourceLanguage}");
        //     }
        //     catch (Exception e)
        //     {
        //         ReportHub.LogError($"FAILED! The test encountered an error: {e.Message}", e);
        //     }
        // }

        private List<string> tests = new List<string>
        {
            "<#00B2FF><link=world>mirko.dcl.eth</link></color>",
            "Hello, my friend! <#00B2FF><link=profile>@Jugurdzija#c9a1</link></color> How are you doing today? I want you to go here: <#00B2FF><link=scene>100,100</link></color> and have a good time",
            "Hello, my friend! How are you doing today? <#00B2FF><link=profile>@Jugurdzija#c9a1</link></color>",
            "Hello world, this is a test of the translation system.<b>Mirko</b>",
            "<#00B2FF><link=profile>@Jugurdzija#c9a1</link></color> Hello",
            "<#00B2FF><link=profile>@Jugurdzija#c9a1</link></color> Hello, my friend! How are you doing today?",
            "Hello <#00B2FF><link=profile>@Jugurdzija#c9a1</link></color> my friend! How are you doing today?",
            "Hello <#00B2FF><link=profile>@Jugurdzija#c9a1</link></color>,<#00B2FF><link=profile>@Jugurdzija#c9a1</link></color> my friends! How are you doing today?",
            "type /help for a list of commands", "hello my friend <#00B2FF><link=profile>@Mirko#5e42</link></color> i am looking forward to talk to you 😾😶 go here please <#00B2FF><link=world>mirko.dcl.eth</link></color>", "😾😶 😾😶 hello 😾😶", "😾😶 <#00B2FF><link=world>mirko.dcl.eth</link></color> 😾",
            "😾😶😾 😾😶 my friend what's up!!! 😾 <#00B2FF><link=profile>@Mirko#5e42</link></color>"

        };

        private List<LanguageCode> languageCodes = new List<LanguageCode>
        {
            LanguageCode.DE,
            LanguageCode.ES,
            LanguageCode.FR,
            LanguageCode.IT,
            LanguageCode.JA,
            LanguageCode.KO,
            LanguageCode.ZH,
            LanguageCode.RU,
            LanguageCode.PT,
            LanguageCode.EN
        };

        [ContextMenu("Test Segmentations")]
        public async void TestSegmentations()
        {
            foreach (var code in languageCodes)
            {

                foreach (string test in tests)
                {
                    string input = test;

                    // 1) Segment
                    var toks = ChatSegmenter.Segment(input);

                    // 2) Extract only text pieces for MT
                    var (pieces, idxs) = ChatSegmenter.ExtractTranslatables(toks);

                    // 3) Translate each piece (or batch if your API supports array 'q')
                    var translated = new string[pieces.Length];
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        var result = await translationProvider.TranslateAsync(pieces[i], code, CancellationToken.None);
                        translated[i] = result.TranslatedText;
                    }

                    // 4) Apply translations back into the token list
                    toks = ChatSegmenter.ApplyTranslations(toks, idxs, translated);

                    // 5) Stitch
                    string output = ChatSegmenter.Stitch(toks);

                    ReportHub.Log(ReportData.UNSPECIFIED, output);
                }
            }
        }

        [ContextMenu("Test Segmentations [Batch]")]
        public async void TestSegmentations2()
        {
            foreach (var code in languageCodes)
            {
                foreach (string test in tests)
                {
                    string input = test;

                    // 1) Segment
                    var toks = ChatSegmenter.Segment(input);

                    // 2) Extract only text pieces for MT
                    var (pieces, idxs) = ChatSegmenter.ExtractTranslatables(toks);

                    // 3) Translate each piece (or batch if your API supports array 'q')
                    var translated = new string[pieces.Length];
                    if (pieces.Length > 0)
                    {
                        var ct = CancellationToken.None;

                        if (translationProvider is IBatchTranslationProvider batchProv)
                        {
                            var arr = await batchProv.TranslateBatchAsync(pieces, code, ct);
                            translated = arr.translatedText;
                        }
                        else
                        {
                            for (int i = 0; i < pieces.Length; i++)
                            {
                                var res = await translationProvider.TranslateAsync(pieces[i], code, ct);
                                translated[i] = res.TranslatedText;
                            }
                        }

                        // 4) Apply translations back into the token list
                        toks = ChatSegmenter.ApplyTranslations(toks, idxs, translated);
                    }

                    // 5) Stitch
                    string output = ChatSegmenter.Stitch(toks);

                    ReportHub.Log(ReportData.UNSPECIFIED, input + "-" + output);
                }
            }
        }
    }
}
*/
