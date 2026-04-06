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
        private readonly Func<float> getBaseStagger;
        private readonly Func<int> getMaxQueueDepth;
        private readonly Func<int> getRampStart;
        private readonly Func<float> getMinStagger;
        private float staggerTimer;

        public int QueueCount => queue.Count;

        public RemoteReactionReceiver(
            Func<float> getBaseStagger,
            Func<int> getMaxQueueDepth,
            Func<int> getRampStart,
            Func<float> getMinStagger,
            Action<ReactionReceivedArgs> onProcessed)
        {
            this.getBaseStagger = getBaseStagger;
            this.getMaxQueueDepth = getMaxQueueDepth;
            this.getRampStart = getRampStart;
            this.getMinStagger = getMinStagger;
            this.onProcessed = onProcessed;
        }

        public void Enqueue(ReactionReceivedArgs args)
        {
            int maxDepth = getMaxQueueDepth();
            int count = Mathf.Clamp(args.Count, 1, MAX_EXPAND);

            for (int i = 0; i < count; i++)
            {
                if (maxDepth > 0 && queue.Count >= maxDepth)
                    break;

                queue.Enqueue(new ReactionReceivedArgs(
                    args.WalletId, args.EmojiIndex, 1,
                    args.Type, args.MessageId, args.IsRemoval));
            }
        }

        public void Tick(float dt)
        {
            float staggerInterval = ComputeEffectiveStagger();

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

        private float ComputeEffectiveStagger()
        {
            float baseStagger = getBaseStagger();
            int maxDepth = getMaxQueueDepth();
            int rampStart = getRampStart();

            if (maxDepth <= 0 || queue.Count <= rampStart)
                return baseStagger;

            float minStagger = getMinStagger();

            if (queue.Count >= maxDepth)
                return minStagger;

            float t = (float)(queue.Count - rampStart) / (maxDepth - rampStart);
            return Mathf.Lerp(baseStagger, minStagger, t);
        }
    }
}
