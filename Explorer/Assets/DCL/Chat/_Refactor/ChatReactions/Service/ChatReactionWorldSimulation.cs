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
            rng = new System.Random();
            atlasTotalTiles = config.Atlas != null ? Mathf.Max(1, config.Atlas.TotalTiles) : 1;

            runtimeMaterial = CreateRuntimeMaterial(config);
            worldPool = new ChatReactionsParticlePool(config.WorldLane.MaxParticles);

            var sizeCurve = config.WorldLane.SizeOverLifetime;
            renderer = new ChatReactionsParticleRenderer(runtimeMaterial, sizeCurve?.length > 0 ? sizeCurve : null);
        }

        public void Dispose()
        {
            if (runtimeMaterial != null)
                UnityEngine.Object.Destroy(runtimeMaterial);
        }

        public void Tick(float dt)
        {
            SimulateParticlePhysics(dt);
            ApplyLateralOscillation(dt);
            EmitStreamParticles(dt);
            EmitDebugNearbyParticles(dt);
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
                SpawnSingleWorldParticle(headPos, emojiIndex, lane);
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

        private void SimulateParticlePhysics(float dt)
        {
            Profiler.BeginSample("ChatReactions.World.Physics");
            aliveCount = worldPool.Tick(dt, config.WorldLane.Gravity, config.WorldLane.Drag);
            Profiler.EndSample();
        }

        private void ApplyLateralOscillation(float dt)
        {
            var lane = config.WorldLane;
            if (lane.ZigZagAmplitude <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.ZigZag");
            var particles = worldPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                float oscillation = Mathf.Sin(p.age * lane.ZigZagFrequency * TWO_PI + p.zigZagPhase)
                                  * lane.ZigZagAmplitude;

                p.vel.x += Mathf.Cos(p.zigZagPhase) * oscillation * dt;
                p.vel.z += Mathf.Sin(p.zigZagPhase) * oscillation * dt;
            }

            Profiler.EndSample();
        }

        private void EmitStreamParticles(float dt)
        {
            if (!isStreaming || streamPositionGetter == null) return;

            Profiler.BeginSample("ChatReactions.World.Stream");
            streamAccumulator += dt * config.WorldLane.StreamRatePerSecond;

            while (streamAccumulator >= 1f)
            {
                streamAccumulator -= 1f;
                EmitSingleStreamBurst();
            }

            Profiler.EndSample();
        }

        private void EmitDebugNearbyParticles(float dt)
        {
            if (!debugActive || debugPositionsGetter == null) return;

            Profiler.BeginSample("ChatReactions.World.DebugNearby");
            debugAccumulator += dt * config.WorldLane.DebugRatePerSecond;

            while (debugAccumulator >= 1f)
            {
                debugAccumulator -= 1f;
                EmitDebugBurstAtAllPositions();
            }

            Profiler.EndSample();
        }

        // ── Spawn Helpers ─────────────────────────────────────────

        private void SpawnSingleWorldParticle(Vector3 headPos, int emojiIndex, ChatReactionsWorldLaneConfig lane)
        {
            float endSize = Rand(lane.SizeRange.x, lane.SizeRange.y);
            float startSize = endSize * Rand(SPAWN_SIZE_MIN_RATIO, SPAWN_SIZE_MAX_RATIO);
            float lifetime = Rand(lane.LifetimeRange.x, lane.LifetimeRange.y);
            float speed = Rand(lane.SpeedRange.x, lane.SpeedRange.y);

            Vector3 spawnPos = ApplyPositionJitter(headPos);
            Vector3 velocity = RandomUpwardVelocity(speed);
            float phase = Rand(0f, TWO_PI);

            worldPool.Spawn(spawnPos, velocity, lifetime, startSize, endSize, emojiIndex, phase);
        }

        private void EmitSingleStreamBurst()
        {
            Vector3? pos = streamPositionGetter!();
            if (!pos.HasValue) return;

            int emoji = ResolveStreamEmojiIndex();
            TriggerWorldReaction(pos.Value, emoji, config.WorldLane.BurstCount);
        }

        private void EmitDebugBurstAtAllPositions()
        {
            var positions = debugPositionsGetter!();

            for (int i = 0; i < positions.Count; i++)
            {
                int emoji = rng.Next(0, atlasTotalTiles);
                TriggerWorldReaction(positions[i], emoji, 1);
            }
        }

        private Vector3 ApplyPositionJitter(Vector3 origin) =>
            origin + new Vector3(
                Rand(-JITTER_XZ, JITTER_XZ),
                Rand(-JITTER_Y, JITTER_Y),
                Rand(-JITTER_XZ, JITTER_XZ));

        private Vector3 RandomUpwardVelocity(float speed) =>
            new (Rand(-JITTER_XZ, JITTER_XZ) * 2f,
                 speed,
                 Rand(-JITTER_XZ, JITTER_XZ) * 2f);

        private int ResolveStreamEmojiIndex() =>
            streamEmojiIndex >= 0 ? streamEmojiIndex : rng.Next(0, atlasTotalTiles);

        private static Material CreateRuntimeMaterial(ChatReactionsSituationalConfig config)
        {
            var mat = new Material(config.EmojiMaterial) { name = config.EmojiMaterial.name + " (World Runtime)" };
            ChatReactionsAtlasHelper.ApplyAtlasToMaterial(mat, config);
            return mat;
        }

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
