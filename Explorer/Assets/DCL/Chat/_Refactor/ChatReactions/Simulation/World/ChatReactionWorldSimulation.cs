using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Chat.ChatReactions.Rendering;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// World-space reaction particle simulation.
    /// Pipeline per tick: Forces -> Integrate -> Compact dead -> Cull visible -> Render.
    /// </summary>
    public sealed class ChatReactionWorldSimulation : IDisposable, IWorldReactionSpawner
    {
        private const float JITTER_XZ = 0.05f;
        private const float JITTER_Y  = 0.02f;
        private const int MAX_DEBUG_NEARBY = 128;

        // Pre-allocated wallet IDs for debug nearby anchors.
        // Avoids string allocation in the hot path while giving each debug
        // avatar a stable anchor so particles are spring-tethered and per-avatar capped.
        private static readonly string[] DEBUG_NEARBY_WALLETS = InitDebugWallets();

        private readonly ChatReactionsConfig config;
        private readonly Material runtimeMaterial;
        private readonly DenseParticleStore<ChatReactionsParticle> store;
        private readonly ChatReactionsParticleRenderer renderer;
        private readonly ParticleVisibilityCuller culler;
        private readonly System.Random rng;
        private readonly int atlasTotalTiles;
        private readonly AvatarAnchorTable anchorTable = new ();
        private readonly IAvatarReactionPosition? avatarPosition;
        private readonly float maxSpawnDistanceSqr;
        private readonly int[] alivePerAnchor = new int[AvatarAnchorTable.MAX_ANCHORS + 1];
        private readonly IWorldParticleForce[] forces;

        private float streamAccumulator;
        private bool isStreaming;
        private Func<Vector3?>? streamPositionGetter;
        private int streamEmojiIndex;
        private string? streamWalletId;

        private float debugAccumulator;
        private bool debugActive;
        private Func<List<Vector3>>? debugPositionsGetter;
        private Action<int, int>? debugNearbyUICallback;

        private int lastVisibleCount;
        private int lastVisibleAnchorCount;
        private int droppedThisFrame;
        private int cappedThisFrame;
        private int lastEffectiveMaxPerAvatar;

        public int AliveCount => store.Count;
        public int PoolCapacity => store.Capacity;
        public int VisibleCount => lastVisibleCount;
        public int VisibleAnchorCount => lastVisibleAnchorCount;
        public bool IsStreaming => isStreaming;
        public bool IsDebugNearbyActive => debugActive;
        public int ActiveAnchorCount => anchorTable.ActiveSlotCount;
        public int AnchorScanLimit => anchorTable.SlotScanLimit;
        public int AnchorSlotCapacity => anchorTable.SlotCapacity;
        public int DroppedThisFrame => droppedThisFrame;
        public int CappedThisFrame => cappedThisFrame;
        public int EffectiveMaxPerAvatar => lastEffectiveMaxPerAvatar;
        public int LocalAnchorAlive { get; private set; }

        public bool HasAliveParticles() => store.Count > 0;

        public ChatReactionWorldSimulation(ChatReactionsConfig config, IAvatarReactionPosition? avatarPosition = null)
        {
            this.config = config;
            this.avatarPosition = avatarPosition;
            rng = new System.Random();
            atlasTotalTiles = config.SafeTotalTiles;

            runtimeMaterial = ChatReactionMaterialFactory.CreateRuntimeMaterial(config, "World Runtime");
            store = new DenseParticleStore<ChatReactionsParticle>(config.WorldLane.MaxParticles);
            culler = new ParticleVisibilityCuller(config.WorldLane.MaxParticles);
            maxSpawnDistanceSqr = config.WorldLane.MaxSpawnDistance * config.WorldLane.MaxSpawnDistance;

            var sizeCurve = config.WorldLane.SizeOverLifetime;
            renderer = new ChatReactionsParticleRenderer(runtimeMaterial, sizeCurve?.length > 0 ? sizeCurve : null);

            forces = new IWorldParticleForce[]
            {
                new AnchorSpringForce(anchorTable, config.WorldLane),
            };
        }

        public void Dispose()
        {
            if (runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(runtimeMaterial);
                else
                    UnityEngine.Object.DestroyImmediate(runtimeMaterial);
            }
        }

        public void Tick(float dt)
        {
            droppedThisFrame = 0;
            cappedThisFrame = 0;

            if (store.Count > 0)
            {
                anchorTable.Refresh(avatarPosition);

                for (int f = 0; f < forces.Length; f++)
                    forces[f].Apply(store.Buffer, store.Count, dt);

                Profiler.BeginSample("ChatReactions.World.Integrate");
                
                ParticleIntegrator.Step(store.Buffer, 
                    store.Count,
                    config.WorldLane.Gravity,
                    config.WorldLane.Drag,
                    dt);
                
                Profiler.EndSample();

                Profiler.BeginSample("ChatReactions.World.Compact");
                
                store.CompactDead();
                
                Profiler.EndSample();
            }

            RefreshAlivePerAnchor();

            EmitStreamParticles(dt);
            EmitDebugNearbyParticles(dt);
        }

        public void Draw(Camera cam)
        {
            if (cam == null || store.Count == 0)
            {
                if (config.DebugEnabled && cam != null)
                {
                    anchorTable.UpdateVisibility(cam, maxSpawnDistanceSqr);
                    lastVisibleAnchorCount = anchorTable.CountVisible();
                }
                else
                {
                    lastVisibleAnchorCount = 0;
                }

                lastVisibleCount = 0;
                return;
            }

            Profiler.BeginSample("ChatReactions.World.Draw");

            anchorTable.UpdateVisibility(cam, maxSpawnDistanceSqr);
            lastVisibleCount = culler.Cull(store.Buffer, store.Count, cam, anchorTable, maxSpawnDistanceSqr);

            if (config.DebugEnabled)
                lastVisibleAnchorCount = anchorTable.CountVisible();

            float zigZagAmp = config.WorldLane.ZigZagAmplitude;
            float zigZagOmega = config.WorldLane.ZigZagFrequency * MathUtils.TWO_PI;
            renderer.Draw(store.Buffer, culler.VisibleIndices, lastVisibleCount,
                config.WorldLane.RenderLayer, 1f, zigZagAmp, zigZagOmega);
            Profiler.EndSample();
        }

        public void TriggerWorldReaction(Vector3 headPos, int emojiIndex, int count)
        {
            var lane = config.WorldLane;
            int n = Mathf.Max(1, count);

            for (int i = 0; i < n; i++)
            {
                if (!SpawnSingleWorldParticle(headPos, emojiIndex, lane))
                {
                    droppedThisFrame++;
                    break;
                }
            }
        }

        public void TriggerAnchoredReaction(Vector3 headPos, string walletId, int emojiIndex, int count)
        {
            byte anchor = anchorTable.Allocate(walletId, headPos);
            var lane = config.WorldLane;
            int maxPerAvatar = ComputeEffectiveMaxPerAvatar(lane.MaxParticlesPerAvatar);
            lastEffectiveMaxPerAvatar = maxPerAvatar;
            int n = Mathf.Max(1, count);

            for (int i = 0; i < n; i++)
            {
                if (maxPerAvatar > 0 && alivePerAnchor[anchor] >= maxPerAvatar)
                {
                    cappedThisFrame++;
                    break;
                }

                if (!SpawnSingleWorldParticle(headPos, emojiIndex, lane, anchor))
                {
                    droppedThisFrame++;
                    break;
                }

                alivePerAnchor[anchor]++;
            }
        }

        public void TriggerAnchoredReactionLocalPlayer(Vector3 headPos, int emojiIndex, int count)
        {
            TriggerAnchoredReaction(headPos, AvatarAnchorTable.LOCAL_PLAYER_ID, emojiIndex, count);
        }

        public void BeginStream(Func<Vector3?> positionGetter, int emojiIndex, string? walletId = null)
        {
            isStreaming = true;
            streamPositionGetter = positionGetter;
            streamEmojiIndex = emojiIndex;
            streamAccumulator = 1f;
            streamWalletId = walletId;
        }

        public void EndStream()
        {
            isStreaming = false;
            streamPositionGetter = null;
            streamAccumulator = 0f;
            streamWalletId = null;
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

        /// <summary>
        /// When set, each debug nearby burst also triggers this callback (emojiIndex, burstCount)
        /// to inject reactions into the UI lane. Only used by /fakereactions.
        /// </summary>
        public void SetDebugNearbyUICallback(Action<int, int>? callback)
        {
            debugNearbyUICallback = callback;
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

        private void RefreshAlivePerAnchor()
        {
            Profiler.BeginSample("ChatReactions.World.AlivePerAnchor");
            Array.Clear(alivePerAnchor, 0, alivePerAnchor.Length);
            var buffer = store.Buffer;
            int count = store.Count;

            for (int i = 0; i < count; i++)
            {
                ref readonly var p = ref buffer[i];

                if (p.anchorIndex != ChatReactionsParticle.ANCHOR_NONE)
                    alivePerAnchor[p.anchorIndex]++;
            }

            LocalAnchorAlive = anchorTable.FindAliveForWallet(AvatarAnchorTable.LOCAL_PLAYER_ID, alivePerAnchor);
            Profiler.EndSample();
        }

        private int ComputeEffectiveMaxPerAvatar(int configuredMax)
        {
            if (!config.DynamicScalingEnabled)
                return configuredMax;

            int activeAnchors = anchorTable.ActiveSlotCount;
            if (activeAnchors <= 0)
                return configuredMax;

            int poolBudget = (int)(store.Capacity * config.WorldPoolTargetUtilization);
            int dynamicMax = poolBudget / activeAnchors;

            return configuredMax > 0
                ? Mathf.Min(configuredMax, dynamicMax)
                : dynamicMax;
        }

        private bool SpawnSingleWorldParticle(Vector3 headPos, int emojiIndex,
            ChatReactionsWorldLaneConfig lane, byte anchorIndex = ChatReactionsParticle.ANCHOR_NONE)
        {
            float endSize = rng.NextFloat(lane.SizeRange.x, lane.SizeRange.y);
            float startSize = endSize * rng.NextFloat(config.SpawnSizeMinRatio, config.SpawnSizeMaxRatio);
            float lifetime = rng.NextFloat(lane.LifetimeRange.x, lane.LifetimeRange.y);
            float speed = rng.NextFloat(lane.SpeedRange.x, lane.SpeedRange.y);

            Vector3 spawnPos = ApplyPositionJitter(headPos);
            Vector3 velocity = RandomUpwardVelocity(speed);
            float phase = rng.NextFloat(0f, MathUtils.TWO_PI);

            return store.TryAdd(new ChatReactionsParticle
            {
                pos = spawnPos,
                vel = velocity,
                age = 0f,
                lifetime = lifetime,
                startSize = startSize,
                endSize = endSize,
                emojiIndex = emojiIndex,
                zigZagPhase = phase,
                alive = 1,
                anchorIndex = anchorIndex,
            });
        }

        private void EmitSingleStreamBurst()
        {
            Vector3? pos = streamPositionGetter!();
            if (!pos.HasValue) return;

            int emoji = ResolveStreamEmojiIndex();

            if (streamWalletId != null)
                TriggerAnchoredReaction(pos.Value, streamWalletId, emoji, config.WorldLane.BurstCount);
            else
                TriggerWorldReaction(pos.Value, emoji, config.WorldLane.BurstCount);
        }

        private void EmitDebugBurstAtAllPositions()
        {
            var positions = debugPositionsGetter!();
            int count = Mathf.Min(positions.Count, MAX_DEBUG_NEARBY);

            for (int i = 0; i < count; i++)
            {
                int emoji = rng.Next(0, atlasTotalTiles);
                TriggerAnchoredReaction(positions[i], DEBUG_NEARBY_WALLETS[i], emoji, config.WorldLane.BurstCount);
                debugNearbyUICallback?.Invoke(emoji, config.WorldLane.BurstCount);
            }
        }

        private static string[] InitDebugWallets()
        {
            var wallets = new string[MAX_DEBUG_NEARBY];

            for (int i = 0; i < MAX_DEBUG_NEARBY; i++)
                wallets[i] = $"__debug_nearby_{i}__";

            return wallets;
        }

        private Vector3 ApplyPositionJitter(Vector3 origin) =>
            origin + new Vector3(
                rng.NextFloat(-JITTER_XZ, JITTER_XZ),
                rng.NextFloat(-JITTER_Y, JITTER_Y),
                rng.NextFloat(-JITTER_XZ, JITTER_XZ));

        private Vector3 RandomUpwardVelocity(float speed) =>
            new (rng.NextFloat(-JITTER_XZ, JITTER_XZ) * 2f,
                 speed,
                 rng.NextFloat(-JITTER_XZ, JITTER_XZ) * 2f);

        private int ResolveStreamEmojiIndex() =>
            streamEmojiIndex >= 0 ? streamEmojiIndex : rng.Next(0, atlasTotalTiles);

    }
}
