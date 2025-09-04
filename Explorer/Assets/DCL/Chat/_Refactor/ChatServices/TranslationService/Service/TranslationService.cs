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

        public void ProcessIncomingMessage(ChatMessage message)
        {
            if (!policy.ShouldAutoTranslate(message, message.MessageId, settings.PreferredLanguage))
            {
                // We don't even need to store it; the default is no translation.
                return;
            }

            var newTranslation = new MessageTranslation(message.Message, settings.PreferredLanguage)
            {
                State = TranslationState.Pending
            };

            translationMemory.Set(message.MessageId, newTranslation);

            eventBus.Publish(new TranslationEvents.MessageTranslationRequested
            {
                MessageId = message.MessageId
            });

            TranslateInternalAsync("", CancellationToken.None).Forget();
        }

        public UniTask TranslateManualAsync(string messageId, CancellationToken ct)
        {
            if (!translationMemory.TryGet(messageId, out var translation)) return UniTask.CompletedTask;

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
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(settings.TranslationTimeoutSeconds));

                var result = await provider.TranslateAsync(translation.OriginalBody, LanguageCode.AutoDetect, targetLang, timeoutCts.Token);

                cache.Set(messageId, targetLang, result);
                translationMemory.SetTranslatedResult(messageId, result);
                eventBus.Publish(new TranslationEvents.MessageTranslated
                {
                    MessageId = messageId
                });
            }
            catch (Exception ex)
            {
                translationMemory.UpdateState(messageId, TranslationState.Failed);
                eventBus.Publish(new TranslationEvents.MessageTranslationFailed
                {
                    MessageId = messageId, Error = ex.Message
                });
            }
        }
    }
}