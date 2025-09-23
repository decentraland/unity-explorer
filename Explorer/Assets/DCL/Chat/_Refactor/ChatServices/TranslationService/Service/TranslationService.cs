using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices.TranslationService.Processors.DCL.Translation.Service.Processing;
using DCL.Chat.ChatServices.TranslationService.Processors.RegEx;
using DCL.Chat.ChatServices.TranslationService.Utilities;
using DCL.Translation.Events;
using DCL.Translation.Models;
using DCL.Translation.Service.Cache;
using DCL.Translation.Service.Memory;
using DCL.Translation.Service.Policy;
using DCL.Translation.Service.Provider;
using DCL.Translation.Settings;
using DCL.Utilities;
using Utility;

namespace DCL.Translation.Service
{
    public class TranslationService : ITranslationService
    {
        private readonly ITranslationProvider provider;
        private readonly IMessageProcessor messageProcessor;
        private readonly ITranslationCache cache;
        private readonly IConversationTranslationPolicy policy;
        private readonly ITranslationSettings settings;
        private readonly IEventBus eventBus;
        private readonly ITranslationMemory translationMemory;

        private static readonly Regex TagRx =
            new(@"<[^>]*>", RegexOptions.Compiled);
        
        public TranslationService(ITranslationProvider provider,
            IMessageProcessor messageProcessor,
            ITranslationCache cache,
            IConversationTranslationPolicy policy,
            ITranslationSettings settings,
            IEventBus eventBus,
            ITranslationMemory translationMemory)
        {
            this.provider = provider;
            this.messageProcessor = messageProcessor;
            this.cache = cache;
            this.policy = policy;
            this.settings = settings;
            this.eventBus = eventBus;
            this.translationMemory = translationMemory;
        }

        public void ProcessIncomingMessage(string messageId, string originalText, string conversationId)
        {
            if (!settings.IsTranslationFeatureActive()) return;

            if (!policy.ShouldAutoTranslate(originalText, conversationId, settings.PreferredLanguage))
            {
                // We don't even need to store it; the default is no translation.
                return;
            }

            var newTranslation = new MessageTranslation(originalText, settings.PreferredLanguage)
            {
                State = TranslationState.Pending
            };

            translationMemory.Set(messageId, newTranslation);

            eventBus.Publish(new TranslationEvents.MessageTranslationRequested
            {
                MessageId = messageId
            });

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TranslationTimeoutSeconds));
            TranslateInternalAsync(messageId, cts.Token).Forget();
        }

        public UniTask TranslateManualAsync(string messageId, string originalText, CancellationToken ct)
        {
            if (!settings.IsTranslationFeatureActive()) return UniTask.CompletedTask;

            // The logic is now much cleaner.
            // 1. Check if a record already exists. If not, create one from the provided text.
            if (!translationMemory.TryGet(messageId, out var translation))
            {
                translation = new MessageTranslation(originalText, settings.PreferredLanguage);
                translationMemory.Set(messageId, translation);
            }

            // 2. Set state to Pending and start the translation.
            translation.State = TranslationState.Pending;
            eventBus.Publish(new TranslationEvents.MessageTranslationRequested
            {
                MessageId = messageId
            });
            return TranslateInternalAsync(messageId, ct);
        }

        public void RevertToOriginal(string messageId)
        {
            translationMemory.UpdateState(messageId, TranslationState.Original);
            eventBus.Publish(new TranslationEvents.MessageTranslationReverted
            {
                MessageId = messageId
            });
        }

        private async UniTask TranslateInternalAsync(string messageId, CancellationToken ct)
        {
            if (!translationMemory.TryGet(messageId, out var translation)) return;

            var targetLang = settings.PreferredLanguage;

            if (cache.TryGet(messageId, targetLang, out var cachedResult))
            {
                translationMemory.SetTranslatedResult(messageId, cachedResult);
                eventBus.Publish(new TranslationEvents.MessageTranslated
                {
                    MessageId = messageId
                });
                return;
            }

            try
            {
                string original = translation.OriginalBody ?? string.Empty;
                
                var result = RequiresProcessing(original)
                    ? await messageProcessor.ProcessAndTranslateAsync(original, targetLang, ct)
                    // ? await UseBatchTranslation(original, targetLang, ct)
                    : await UseRegularTranslation(original, targetLang, ct);

                cache.Set(messageId, targetLang, result);
                translationMemory.SetTranslatedResult(messageId, result);
                eventBus.Publish(new TranslationEvents.MessageTranslated
                {
                    MessageId = messageId
                });
            }
            catch (OperationCanceledException)
            {
                // This is now only caught if the calling context cancels the operation.
                translationMemory.UpdateState(messageId, TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = "Translation was cancelled."
                });
            }
            catch (Exception ex)
            {
                // This catches other provider errors.
                translationMemory.UpdateState(messageId, TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = ex.Message
                });
            }
        }

        private async UniTask<TranslationResult> UseRegularTranslation(
            string text, LanguageCode target, CancellationToken ct)
        {
            var single = await provider.TranslateAsync(text, target, ct);
            return single;
        }


        // /// <summary>
        // ///     NOTE: Segment → translate only TEXT → stitch
        // /// </summary>
        // /// <param name="text"></param>
        // /// <param name="target"></param>
        // /// <param name="ct"></param>
        // /// <returns></returns>
        // private async UniTask<TranslationResult> UseBatchTranslation(
        //     string text, LanguageCode target, CancellationToken ct)
        // {
        //     TranslationDebug.LogInfo($"[Seg] input: \"{text}\"");
        //     
        //     var toks = ChatSegmenter.SegmentByAngleBrackets(text);
        //     TranslationDebug.LogTokens("angle-split", toks);
        //     
        //     toks = ChatSegmenter.ProtectLinkInners(toks);
        //     TranslationDebug.LogTokens("after-protect-link-inners", toks);
        //
        //     toks = ChatSegmenter.SplitTextTokensOnEmoji(toks);
        //     TranslationDebug.LogTokens("after-split-emoji", toks);
        //
        //     toks = ChatSegmenter.SplitTextTokensOnNumbersAndDates(toks);
        //     TranslationDebug.LogTokens("after-numbers-and-dates", toks);
        //
        //     toks = ChatSegmenter.SplitTextTokensOnSlashCommands(toks);
        //     TranslationDebug.LogTokens("after-backslash-commands", toks);
        //
        //     (string[] cores, int[] idxs, string[] leading, string[] trailing) =
        //         ChatSegmenter.ExtractTranslatablesPreserveSpaces(toks);
        //
        //     string[] translated = Array.Empty<string>();
        //     if (cores.Length > 0)
        //     {
        //         if (provider is IBatchTranslationProvider batchProv)
        //         {
        //             var resp = await batchProv.TranslateBatchAsync(cores, target, ct);
        //             translated = resp.translatedText;
        //         }
        //         else
        //         {
        //             translated = new string[cores.Length];
        //             for (int i = 0; i < cores.Length; i++)
        //             {
        //                 var r = await provider.TranslateAsync(cores[i], target, ct);
        //                 translated[i] = r.TranslatedText;
        //             }
        //         }
        //
        //         toks = ChatSegmenter.ApplyTranslationsWithSpaces(toks, idxs, leading, trailing, translated);
        //     }
        //
        //     TranslationDebug.LogTokens("after-apply", toks);
        //     
        //     string stitched = ChatSegmenter.Stitch(toks);
        //     TranslationDebug.LogInfo($"[Seg] output: \"{stitched}\"");
        //     
        //     return new TranslationResult(stitched, LanguageCode.EN, false);
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlashCommandMessage(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                   ProtectedPatterns.FullLineSlashCommandRx.IsMatch(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresProcessing(string text)
        {
            // Ignore translation for empty or null strings
            if (string.IsNullOrEmpty(text)) return false;

            // Ignore translation if it's pure command
            if (IsSlashCommandMessage(text)) return false;

            // Any TMP-style tag? -> yes, batch
            if (TagRx.IsMatch(text)) return true;

            // Any emoji in the string? -> yes, batch
            // (uses your EmojiDetector with ZWJ/VS-16 support)
            if (EmojiDetector.FindEmoji(text).Count > 0) return true;

            // Any dates or currencies? -> yes, batch
            if (ProtectedPatterns.HasProtectedNumericOrTemporal(text)) return true;

            // Any commands with backslash? -> yes, batch
            if (ProtectedPatterns.InlineCommandRx.IsMatch(text)) return true;

            return false;
        }
    }
}
