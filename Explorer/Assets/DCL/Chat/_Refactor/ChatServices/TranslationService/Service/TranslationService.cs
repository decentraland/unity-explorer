using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatConfig;
using DCL.Chat.History;
using DCL.Translation.Events;
using DCL.Translation.Models;
using DCL.Translation.Service.Cache;
using DCL.Translation.Service.Memory;
using DCL.Translation.Service.Policy;
using DCL.Translation.Service.Provider;
using DCL.Translation.Settings;
using Utility;

namespace DCL.Translation.Service
{
    public class TranslationService : ITranslationService
    {
        private readonly ITranslationProvider provider;
        private readonly ITranslationCache cache;
        private readonly IConversationTranslationPolicy policy;
        private readonly ITranslationSettings settings;
        private readonly IEventBus eventBus;
        private readonly ITranslationMemory translationMemory;

        public TranslationService(ITranslationProvider provider,
            ITranslationCache cache,
            IConversationTranslationPolicy policy,
            ITranslationSettings settings,
            IEventBus eventBus,
            ITranslationMemory translationMemory)
        {
            this.provider = provider;
            this.cache = cache;
            this.policy = policy;
            this.settings = settings;
            this.eventBus = eventBus;
            this.translationMemory = translationMemory;
        }

        public  void ProcessIncomingMessage(string messageId, string originalText, string conversationId)
        {
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

            TranslateInternalAsync(messageId, CancellationToken.None).Forget();
        }

        public UniTask TranslateManualAsync(string messageId, string originalText, CancellationToken ct)
        {
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

            CancellationTokenSource timeoutCts = null;
            try
            {
                // 1. Create the CTS without a 'using' statement.
                // We link it to the incoming cancellation token.
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                // 2. Schedule the timeout.
                timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(settings.TranslationTimeoutSeconds));

                // 3. Await the translation using the new token.
                var result = await provider.TranslateAsync(translation.OriginalBody, LanguageCode.AutoDetect, targetLang, timeoutCts.Token);

                // 4. Process success.
                cache.Set(messageId, targetLang, result);
                translationMemory.SetTranslatedResult(messageId, result);
                eventBus.Publish(new TranslationEvents.MessageTranslated
                {
                    MessageId = messageId
                });
            }
            catch (OperationCanceledException)
            {
                // This will be caught if our timeout is triggered OR if the parent 'ct' is canceled.
                // It's a "normal" failure, not a critical error.
                translationMemory.UpdateState(messageId, TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = "Translation timed out."
                });
            }
            catch (Exception ex)
            {
                // This catches other errors, like the mock provider's simulated failure.
                translationMemory.UpdateState(messageId, TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = ex.Message
                });
            }
            finally
            {
                // 5. CRUCIAL: Always dispose of the CTS when we are done.
                // The 'finally' block guarantees this runs, preventing memory leaks.
                timeoutCts?.Dispose();
            }
        }
    }
}