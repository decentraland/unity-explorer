using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Translation.Processors;
using DCL.Translation.Processors.DCL.Translation.Service.Processing;
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

        // A thread-safe dictionary to
        // hold a lock for each user's translation queue.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> userTranslationLocks = new ();
        
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

        public void ProcessIncomingMessage(string messageId, string senderWalletId, string originalText, string conversationId)
        {
            if (!settings.IsTranslationFeatureActive()) return;

            if (!policy.ShouldAutoTranslate(originalText, conversationId, settings.PreferredLanguage))
            {
                // We don't even need to store it; the default is no translation.
                return;
            }

            // NOTE: Start the translation process without blocking the caller.
            ProcessQueuedTranslationRequestAsync(messageId, senderWalletId, originalText).Forget();
        }

        private async UniTaskVoid ProcessQueuedTranslationRequestAsync(string messageId, string senderWalletId, string originalText)
        {
            var newTranslation = new MessageTranslation(originalText, settings.PreferredLanguage, TranslationState.Pending);
            translationMemory.Set(messageId, newTranslation);
            
            eventBus.Publish(new TranslationEvents.MessageTranslationRequested
            {
                MessageId = messageId, Translation = newTranslation
            });

            async UniTask PerformTranslationAsync()
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TranslationTimeoutSeconds));
                await TranslateInternalAsync(messageId, cts.Token);
            }

            if (!string.IsNullOrEmpty(senderWalletId))
            {
                var userLock = userTranslationLocks.GetOrAdd(senderWalletId, _ => new SemaphoreSlim(1, 1));

                // Wait for our turn
                await userLock.WaitAsync();
                try
                {
                    // We now await the entire translation process
                    // The lock will be held until this is complete.
                    await PerformTranslationAsync();
                }
                finally
                {
                    // This now runs only AFTER the translation has finished.
                    userLock.Release();
                }
            }
            else
            {
                // No wallet ID, so we don't queue. Translate immediately.
                await PerformTranslationAsync();
            }
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
            translation.UpdateState(TranslationState.Pending);
            eventBus.Publish(new TranslationEvents.MessageTranslationRequested
            {
                MessageId = messageId, Translation = translation
            });
            return TranslateInternalAsync(messageId, ct);
        }

        
        
        public void RevertToOriginal(string messageId)
        {
            if (!translationMemory.TryGet(messageId, out var translation))
            {
                return;
            }

            translation.RevertToOriginal();

            eventBus.Publish(new TranslationEvents.MessageTranslationReverted
            {
                MessageId = messageId, Translation = translation
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
                    MessageId = messageId, Translation = translation
                });
                return;
            }

            try
            {
                string original = translation.OriginalBody ?? string.Empty;
                
                var result = RequiresProcessing(original)
                    ? await messageProcessor.ProcessAndTranslateAsync(original, targetLang, ct)
                    : await UseRegularTranslationAsync(original, targetLang, ct);
                
                if (result.DetectedSourceLanguage == targetLang)
                {
                    // Since TranslationResult is a struct, we create a new instance,
                    // replacing the translated text with the original body.
                    result = new TranslationResult(original, result.DetectedSourceLanguage, result.FromCache);
                }
                
                cache.Set(messageId, targetLang, result);
                translationMemory.SetTranslatedResult(messageId, result);
                eventBus.Publish(new TranslationEvents.MessageTranslated
                {
                    MessageId = messageId, Translation = translation
                });
            }
            catch (OperationCanceledException)
            {
                // This is now only caught if the calling context cancels the operation.
                translation.UpdateState(TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = "Translation was cancelled.", Translation = translation
                });
            }
            catch (Exception ex)
            {
                // This catches other provider errors.
                translation.UpdateState(TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = ex.Message, Translation = translation
                });
            }
        }

        private async UniTask<TranslationResult> UseRegularTranslationAsync(
            string text, LanguageCode target, CancellationToken ct)
        {
            var single = await provider.TranslateAsync(text, target, ct);
            return single;
        }

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
