using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Pure C# simulation for the situational reaction particle system.
    /// Particles live in screen space (pixels) so they do not move with the camera.
    /// Owns the particle pool, GPU renderer, spawn resolver, and stream emitter.
    /// </summary>
    public sealed class ChatReactionSimulation : IDisposable
    {
        private const float LANE_JITTER_H = 12f;
        private const float LANE_JITTER_V = 8f;
        private const float RECT_JITTER_H = 8f;
        private const float RECT_JITTER_V = 4f;
        private const float FALLBACK_LATERAL_RANGE = 20f;
        private const float SPAWN_SIZE_MIN_RATIO = 0.2f;
        private const float SPAWN_SIZE_MAX_RATIO = 0.5f;

        private readonly ChatReactionsConfig config;
        private readonly Material runtimeMaterial;
        private readonly ChatReactionsUiParticlePool uiPool;
        private readonly ChatReactionsParticleRenderer renderer;
        private readonly UIReactionSpawnResolver spawnResolver;
        private readonly UIReactionStreamEmitter streamEmitter;
        private readonly ChatReactionFlightController? flightController;
        private readonly System.Random rng;
        private readonly int atlasTotalTiles;

        private RectTransform? defaultSpawnRect;
        private int uiAliveCount;

        public int AliveCount => uiAliveCount;
        public int PoolCapacity => uiPool.Capacity;
        public bool IsStreaming => streamEmitter.IsStreaming;

        public void SetDefaultSpawnRect(RectTransform rect) { defaultSpawnRect = rect; }

        public ChatReactionSimulation(ChatReactionsConfig config, RectTransform laneRect)
        {
            this.config = config;
            rng = new System.Random();
            atlasTotalTiles = config.Atlas != null ? Mathf.Max(1, config.Atlas.TotalTiles) : 1;

            runtimeMaterial = CreateRuntimeMaterial(config);
            uiPool = new ChatReactionsUiParticlePool(config.UILane.MaxParticles);
            spawnResolver = new UIReactionSpawnResolver(laneRect);
            streamEmitter = new UIReactionStreamEmitter();

            if (config.UILane.UseFlightPath)
                flightController = new ChatReactionFlightController(config.UILane, rng);

            renderer = new ChatReactionsParticleRenderer(
                runtimeMaterial,
                config.UILane.UseFlightPath ? config.UILane.SizeOverLifetime : null);
        }

        public void Dispose()
        {
            if (runtimeMaterial != null)
                UnityEngine.Object.Destroy(runtimeMaterial);
        }

        public void Tick(float dt)
        {
            SimulateParticlePhysics(dt);
            ApplyFlightSteering(dt);
            EmitStreamParticles(dt);
        }

        public void Draw(Camera cam)
        {
            if (cam == null) return;

            Profiler.BeginSample("ChatReactions.UI.Draw");
            renderer.Draw(cam, uiPool.Raw, config.UILane.RenderLayer, config.UILane.DepthFromCamera);
            Profiler.EndSample();
        }

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            Vector2 basePx = ResolveDefaultSpawnPosition();
            SpawnBurst(basePx, emojiIndex, count, LANE_JITTER_H, LANE_JITTER_V);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            Vector2 basePx = spawnResolver.GetSpawnPxFromRectCenter(sourceRect);
            SpawnBurst(basePx, emojiIndex, count, RECT_JITTER_H, RECT_JITTER_V);
        }

        public void TriggerDefaultUIReaction()
        {
            TriggerUIReaction(ResolveDefaultEmojiIndex(), config.UILane.StreamBurst);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            TriggerUIReactionFromRect(sourceRect, ResolveDefaultEmojiIndex(), config.UILane.StreamBurst);
        }

        public void BeginUIStream(RectTransform sourceRect) =>
            streamEmitter.Begin(sourceRect);

        public void EndUIStream() =>
            streamEmitter.End();

        public void ToggleUIStream(RectTransform sourceRect) =>
            streamEmitter.Toggle(sourceRect);

#if UNITY_EDITOR || DEBUG
        public void BeginDebugUIStream(RectTransform? sourceRect = null) => streamEmitter.Begin(sourceRect);
        public void EndDebugUIStream() => streamEmitter.End();
#endif

        private void SimulateParticlePhysics(float dt)
        {
            Profiler.BeginSample("ChatReactions.UI.Physics");
            uiAliveCount = uiPool.Update(dt, config.UILane.Gravity, config.UILane.Drag);
            Profiler.EndSample();
        }

        private void ApplyFlightSteering(float dt)
        {
            if (flightController == null) return;

            Profiler.BeginSample("ChatReactions.UI.FlightSteer");
            var particles = uiPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                Vector2 accel = flightController.GetSteering2D(p.age, p.zigZagPhase);
                p.screenVel += accel * dt;
            }

            Profiler.EndSample();
        }

        private void EmitStreamParticles(float dt)
        {
            int ticks = streamEmitter.Tick(dt, config.UILane.StreamRatePerSecond);
            if (ticks == 0) return;

            Profiler.BeginSample("ChatReactions.UI.Stream");

            for (int t = 0; t < ticks; t++)
            {
                int index = ResolveDefaultEmojiIndex();
                RectTransform? source = streamEmitter.Source;

                if (source != null)
                    TriggerUIReactionFromRect(source, index, config.UILane.StreamBurst);
                else
                    TriggerUIReaction(index, config.UILane.StreamBurst);
            }

            Profiler.EndSample();
        }

        // ── Spawn Helpers ─────────────────────────────────────────

        private void SpawnBurst(Vector2 basePx, int emojiIndex, int count, float jitterH, float jitterV)
        {
            var ui = config.UILane;

            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float endSizePx = Rand(ui.SizeRange.x, ui.SizeRange.y);
                float startSizePx = endSizePx * Rand(SPAWN_SIZE_MIN_RATIO, SPAWN_SIZE_MAX_RATIO);
                float lifetime = Rand(ui.LifetimeRange.x, ui.LifetimeRange.y);

                Vector2 velocity = ResolveSpawnVelocity(ui, out float phase);
                Vector2 jitter = new Vector2(Rand(-jitterH, jitterH), Rand(-jitterV, jitterV));

                uiPool.Spawn(basePx + jitter, velocity, lifetime, startSizePx, endSizePx, emojiIndex, phase);
            }
        }

        private Vector2 ResolveSpawnVelocity(ChatReactionsUILaneConfig ui, out float phase)
        {
            if (flightController != null)
            {
                phase = flightController.GetRandomPhase();
                return flightController.GetSpawnVelocity2D();
            }

            phase = 0f;
            float speed = Rand(ui.SpeedRange.x, ui.SpeedRange.y);
            return new Vector2(Rand(-FALLBACK_LATERAL_RANGE, FALLBACK_LATERAL_RANGE), speed);
        }

        private Vector2 ResolveDefaultSpawnPosition() =>
            defaultSpawnRect != null
                ? spawnResolver.GetSpawnPxFromRectCenter(defaultSpawnRect)
                : spawnResolver.GetSpawnPxBottomCenter();

        private int ResolveDefaultEmojiIndex() =>
            config.UILane.RandomEmoji
                ? rng.Next(0, atlasTotalTiles)
                : config.UILane.DefaultEmojiIndex;

        private static Material CreateRuntimeMaterial(ChatReactionsConfig config)
        {
            var mat = new Material(config.EmojiMaterial) { name = config.EmojiMaterial.name + " (Runtime)" };
            ChatReactionsAtlasHelper.ApplyAtlasToMaterial(mat, config);
            return mat;
        }

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
