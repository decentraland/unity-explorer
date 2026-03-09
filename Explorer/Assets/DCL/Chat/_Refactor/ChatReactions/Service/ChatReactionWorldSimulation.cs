using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Pure C# simulation for world-space situational reaction particles.
    /// Particles are placed at real world-space positions (e.g. above avatar heads)
    /// and rendered as camera-facing billboards that respect scene depth.
    /// </summary>
    public sealed class ChatReactionWorldSimulation : IDisposable
    {
        private const float SPAWN_SIZE_MIN_RATIO = 0.2f;
        private const float SPAWN_SIZE_MAX_RATIO = 0.5f;
        private const float JITTER_XZ = 0.05f;
        private const float JITTER_Y  = 0.02f;
        private const float TWO_PI = Mathf.PI * 2f;

        private readonly ChatReactionsSituationalConfig config;
        private readonly Material runtimeMaterial;
        private readonly ChatReactionsParticlePool worldPool;
        private readonly ChatReactionsParticleRenderer renderer;
        private readonly System.Random rng;
        private readonly int atlasTotalTiles;

        private int aliveCount;
        private float streamAccumulator;
        private bool isStreaming;
        private Func<Vector3?>? streamPositionGetter;
        private int streamEmojiIndex;

        private float debugAccumulator;
        private bool debugActive;
        private Func<List<Vector3>>? debugPositionsGetter;

        public int AliveCount => aliveCount;
        public int PoolCapacity => worldPool.Capacity;
        public bool IsStreaming => isStreaming;
        public bool IsDebugNearbyActive => debugActive;

        public ChatReactionWorldSimulation(ChatReactionsSituationalConfig config)
        {
            this.config = config;

            runtimeMaterial = new Material(config.EmojiMaterial) { name = config.EmojiMaterial.name + " (World Runtime)" };
            ChatReactionsAtlasHelper.ApplyAtlasToMaterial(runtimeMaterial, config);

            worldPool = new ChatReactionsParticlePool(config.WorldLane.MaxParticles);

            var sizeCurve = config.WorldLane.SizeOverLifetime;
            renderer = new ChatReactionsParticleRenderer(runtimeMaterial, sizeCurve?.length > 0 ? sizeCurve : null);
            rng = new System.Random();
            atlasTotalTiles = config.Atlas != null ? Mathf.Max(1, config.Atlas.TotalTiles) : 1;
        }

        public void Dispose()
        {
            if (runtimeMaterial != null)
                UnityEngine.Object.Destroy(runtimeMaterial);
        }

        public void Tick(float dt)
        {
            Profiler.BeginSample("ChatReactions.World.PoolTick");
            aliveCount = worldPool.Tick(dt, config.WorldLane.Gravity, config.WorldLane.Drag);
            Profiler.EndSample();

            Profiler.BeginSample("ChatReactions.World.ZigZag");
            TickZigZag(dt);
            Profiler.EndSample();

            Profiler.BeginSample("ChatReactions.World.Stream");
            TickStream(dt);
            Profiler.EndSample();

            Profiler.BeginSample("ChatReactions.World.DebugNearby");
            TickDebug(dt);
            Profiler.EndSample();
        }

        public void BeginStream(Func<Vector3?> positionGetter, int emojiIndex)
        {
            isStreaming = true;
            streamPositionGetter = positionGetter;
            streamEmojiIndex = emojiIndex;
            streamAccumulator = 1f;
        }

        public void EndStream()
        {
            isStreaming = false;
            streamPositionGetter = null;
            streamAccumulator = 0f;
        }

        public void BeginDebugNearby(Func<List<Vector3>> positionsGetter)
        {
            debugActive = true;
            debugPositionsGetter = positionsGetter;
            debugAccumulator = 1f;
        }

        public void EndDebugNearby()
        {
            debugActive = false;
            debugPositionsGetter = null;
            debugAccumulator = 0f;
        }

        private void TickStream(float dt)
        {
            if (!isStreaming || streamPositionGetter == null) return;

            streamAccumulator += dt * config.WorldLane.StreamRatePerSecond;

            while (streamAccumulator >= 1f)
            {
                streamAccumulator -= 1f;

                Vector3? pos = streamPositionGetter();
                if (!pos.HasValue) continue;

                int emoji = streamEmojiIndex >= 0 ? streamEmojiIndex : rng.Next(0, atlasTotalTiles);
                TriggerWorldReaction(pos.Value, emoji, config.WorldLane.BurstCount);
            }
        }

        private void TickDebug(float dt)
        {
            if (!debugActive || debugPositionsGetter == null) return;

            debugAccumulator += dt * config.WorldLane.DebugRatePerSecond;

            while (debugAccumulator >= 1f)
            {
                debugAccumulator -= 1f;

                var positions = debugPositionsGetter();

                for (int i = 0; i < positions.Count; i++)
                {
                    int emoji = rng.Next(0, atlasTotalTiles);
                    TriggerWorldReaction(positions[i], emoji, 1);
                }
            }
        }

        public void Draw(Camera cam)
        {
            if (cam == null || aliveCount == 0) return;

            Profiler.BeginSample("ChatReactions.World.Draw");
            renderer.Draw(worldPool.Raw, config.WorldLane.RenderLayer);
            Profiler.EndSample();
        }

        /// <summary>
        /// Spawns a burst of emoji particles rising upward from <paramref name="headPos"/>
        /// (typically <c>AvatarBase.GetAdaptiveNametagPosition()</c>).
        /// </summary>
        public void TriggerWorldReaction(Vector3 headPos, int emojiIndex, int count)
        {
            var lane = config.WorldLane;

            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float endSize = Rand(lane.SizeRange.x,    lane.SizeRange.y);
                float startSize = endSize * Rand(SPAWN_SIZE_MIN_RATIO, SPAWN_SIZE_MAX_RATIO);
                float lifetime = Rand(lane.LifetimeRange.x, lane.LifetimeRange.y);
                float speed = Rand(lane.SpeedRange.x,    lane.SpeedRange.y);

                Vector3 spawnPos = headPos + new Vector3(
                    Rand(-JITTER_XZ, JITTER_XZ),
                    Rand(-JITTER_Y,  JITTER_Y),
                    Rand(-JITTER_XZ, JITTER_XZ));

                Vector3 vel = new Vector3(
                    Rand(-JITTER_XZ, JITTER_XZ) * 2f,
                    speed,
                    Rand(-JITTER_XZ, JITTER_XZ) * 2f);

                float phase = Rand(0f, TWO_PI);
                worldPool.Spawn(spawnPos, vel, lifetime, startSize, endSize, emojiIndex, phase);
            }
        }

        private void TickZigZag(float dt)
        {
            var lane = config.WorldLane;
            if (lane.ZigZagAmplitude <= 0f) return;

            var particles = worldPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                float oscillation = Mathf.Sin(p.age * lane.ZigZagFrequency * TWO_PI + p.zigZagPhase)
                                  * lane.ZigZagAmplitude;

                // Each particle oscillates in a unique horizontal direction based on its phase
                p.vel.x += Mathf.Cos(p.zigZagPhase) * oscillation * dt;
                p.vel.z += Mathf.Sin(p.zigZagPhase) * oscillation * dt;
            }
        }

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
