using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Receives remote situational reactions and plays them back as independent
    /// per-avatar cascades. Each remote avatar gets its own queue + drain timer,
    /// so bursts from many avatars render in parallel instead of interleaving
    /// through a single global pipe.
    ///
    /// Bounding:
    /// - World spawns are bounded by MaxParticlesPerAvatar + world pool capacity.
    /// - UI spawns are bounded by UILane.MaxVisibleParticles.
    /// - Per-avatar queue is bounded by MaxPerAvatarQueued (oldest-drop safety cap).
    /// </summary>
    internal sealed class SituationalRemoteTarget : IRemoteReactionTarget
    {
        private readonly ChatReactionsConfig config;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly ChatReactionUISimulation uiSimulation;

        private readonly Dictionary<string, PerAvatarCascade> cascades = new (32);
        private readonly Stack<PerAvatarCascade> cascadePool = new (32);
        private readonly List<string> drainedScratch = new (32);

        public bool ShowRemoteUIReactions { get; set; } = true;

        public event Action<ReactionReceivedArgs>? RemoteReactionProcessed;

        public SituationalRemoteTarget(
            ChatReactionsConfig config,
            LocalPlayerWorldReactor worldReactor,
            ChatReactionUISimulation uiSimulation)
        {
            this.config = config;
            this.worldReactor = worldReactor;
            this.uiSimulation = uiSimulation;
        }

        public void HandleRemoteReaction(ReactionReceivedArgs args)
        {
            Profiler.BeginSample("ChatReactions.Remote.Handle");

            int batchCap = config.MessageReactions.NetworkFlushThreshold;
            int count = batchCap > 0 ? Mathf.Min(args.Count, batchCap) : Mathf.Max(args.Count, 1);

            if (count > 0)
            {
                PerAvatarCascade cascade = GetOrCreateCascade(args.WalletId);
                int perAvatarCap = config.MaxPerAvatarQueued;

                for (int i = 0; i < count; i++)
                {
                    if (perAvatarCap > 0 && cascade.Particles.Count >= perAvatarCap)
                        cascade.Particles.Dequeue();

                    cascade.Particles.Enqueue(args.EmojiIndex);
                }
            }

            Profiler.EndSample();
        }

        public void Tick(float dt)
        {
            if (cascades.Count == 0) return;

            Profiler.BeginSample("ChatReactions.Remote.Drain");

            float interval = config.SituationalReceiveStaggerInterval;

            foreach (var kvp in cascades)
            {
                PerAvatarCascade cascade = kvp.Value;

                if (interval <= 0f)
                {
                    while (cascade.Particles.Count > 0)
                        ProcessOneParticle(kvp.Key, cascade.Particles.Dequeue());
                }
                else
                {
                    cascade.Timer -= dt;

                    while (cascade.Timer <= 0f && cascade.Particles.Count > 0)
                    {
                        ProcessOneParticle(kvp.Key, cascade.Particles.Dequeue());
                        cascade.Timer += interval;
                    }
                }

                if (cascade.Particles.Count == 0)
                    drainedScratch.Add(kvp.Key);
            }

            for (int i = 0; i < drainedScratch.Count; i++)
            {
                string walletId = drainedScratch[i];

                if (cascades.TryGetValue(walletId, out PerAvatarCascade drained))
                {
                    cascades.Remove(walletId);
                    ReturnCascade(drained);
                }
            }

            drainedScratch.Clear();

            Profiler.EndSample();
        }

        private void ProcessOneParticle(string walletId, int emojiIndex)
        {
            worldReactor.TriggerRemoteBurst(walletId, emojiIndex, 1);

            if (ShowRemoteUIReactions)
                uiSimulation.TriggerUIReaction(emojiIndex, 1);

            if (RemoteReactionProcessed != null)
                RemoteReactionProcessed.Invoke(new ReactionReceivedArgs(
                    walletId, emojiIndex, 1, ReactionType.Situational, string.Empty));
        }

        private PerAvatarCascade GetOrCreateCascade(string walletId)
        {
            if (cascades.TryGetValue(walletId, out PerAvatarCascade existing))
                return existing;

            PerAvatarCascade cascade = cascadePool.Count > 0 ? cascadePool.Pop() : new PerAvatarCascade();
            cascade.Timer = 0f;
            cascades[walletId] = cascade;
            return cascade;
        }

        private void ReturnCascade(PerAvatarCascade cascade)
        {
            cascade.Particles.Clear();
            cascade.Timer = 0f;
            cascadePool.Push(cascade);
        }

        private sealed class PerAvatarCascade
        {
            public readonly Queue<int> Particles = new (16);
            public float Timer;
        }
    }
}
