#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Editor-only runtime debug panel for chat reactions.
    /// All rendering is handled by ChatReactionDebugViewEditor.
    /// This component only holds state and subscribes to events.
    /// </summary>
    public sealed class ChatReactionDebugView : MonoBehaviour, IDisposable
    {
        private const int LOG_CAPACITY = 32;

        private IChatReactionEventBus? eventBus;
        private bool initialized;

        // --- Public accessors for the custom editor ---

        public ChatReactionsConfig? Config { get; private set; }
        public bool Initialized => initialized;

        public readonly Queue<ReactionSentEvent> SentLog = new (LOG_CAPACITY);
        public readonly Queue<ReactionReceivedEvent> ReceivedLog = new (LOG_CAPACITY);

        public ChatReactionStats LastStats
        {
            get
            {
                var state = ChatReactionDebugState.Current;
                return state?.LastStats ?? default;
            }
        }

        public void Init(ChatReactionsConfig config, IChatReactionEventBus bus)
        {
            Config = config;
            eventBus = bus;

            eventBus.ReactionSent += OnReactionSent;
            eventBus.ReactionReceived += OnReactionReceived;
            eventBus.ReactionFlushed += OnReactionFlushed;

            initialized = true;
        }

        public void Dispose()
        {
            UnsubscribeEventBus();

            if (gameObject != null)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            UnsubscribeEventBus();
        }

        private void OnReactionSent(ReactionSentEvent e)
        {
            EnqueueCapped(SentLog, e);
        }

        private void OnReactionReceived(ReactionReceivedEvent e)
        {
            EnqueueCapped(ReceivedLog, e);
        }

        private void OnReactionFlushed(ReactionFlushedEvent e)
        {
            EnqueueCapped(SentLog, new ReactionSentEvent(-1, e.TotalCount, e.Timestamp, ReactionType.Situational));
        }

        private static void EnqueueCapped<T>(Queue<T> queue, T item)
        {
            if (queue.Count >= LOG_CAPACITY)
                queue.Dequeue();

            queue.Enqueue(item);
        }

        private void UnsubscribeEventBus()
        {
            if (eventBus != null)
            {
                eventBus.ReactionSent -= OnReactionSent;
                eventBus.ReactionReceived -= OnReactionReceived;
                eventBus.ReactionFlushed -= OnReactionFlushed;
                eventBus = null;
            }

            initialized = false;
        }
    }
}
#endif
