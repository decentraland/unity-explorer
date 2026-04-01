using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Networking;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Buffers incoming remote reactions and drains them at a configurable stagger
    /// interval to create a visual cascade instead of all reactions appearing at once.
    /// </summary>
    internal sealed class RemoteReactionReceiver
    {
        private const int MAX_EXPAND = 20;

        private readonly Queue<ReactionReceivedArgs> queue = new (MAX_EXPAND);
        private readonly Action<ReactionReceivedArgs> onProcessed;
        private readonly Func<float> getStaggerInterval;
        private float staggerTimer;

        public RemoteReactionReceiver(Func<float> getStaggerInterval, Action<ReactionReceivedArgs> onProcessed)
        {
            this.getStaggerInterval = getStaggerInterval;
            this.onProcessed = onProcessed;
        }

        public void Enqueue(ReactionReceivedArgs args)
        {
            int count = Mathf.Clamp(args.Count, 1, MAX_EXPAND);

            for (int i = 0; i < count; i++)
            {
                queue.Enqueue(new ReactionReceivedArgs(
                    args.WalletId, args.EmojiIndex, 1,
                    args.Type, args.MessageId, args.IsRemoval));
            }
        }

        public void Tick(float dt)
        {
            float staggerInterval = getStaggerInterval();

            if (staggerInterval <= 0f)
            {
                while (queue.Count > 0)
                    onProcessed(queue.Dequeue());

                return;
            }

            if (queue.Count == 0)
            {
                staggerTimer = 0f;
                return;
            }

            staggerTimer -= dt;

            while (staggerTimer <= 0f && queue.Count > 0)
            {
                onProcessed(queue.Dequeue());
                staggerTimer += staggerInterval;
            }
        }
    }
}
