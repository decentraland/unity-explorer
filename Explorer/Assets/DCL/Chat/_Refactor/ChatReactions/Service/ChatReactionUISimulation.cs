using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Orchestrates the screen-space UI particle pipeline:
    /// Store → Flight steering → Integrate → Compact → Render.
    /// Owns spawning, streaming, and spawn resolution logic.
    /// </summary>
    public sealed class ChatReactionUISimulation : IDisposable
    {
        private const float LANE_JITTER_H = 12f;
        private const float LANE_JITTER_V = 8f;
        private const float RECT_JITTER_H = 8f;
        private const float RECT_JITTER_V = 4f;
        private const float FALLBACK_LATERAL_RANGE = 20f;

        private readonly ChatReactionsConfig config;
        private readonly Material runtimeMaterial;
        private readonly DenseParticleStore<ChatReactionsUiParticle> uiStore;
        private readonly ChatReactionsParticleRenderer renderer;
        private readonly UIReactionSpawnResolver spawnResolver;
        private readonly UIReactionStreamEmitter streamEmitter;
        private readonly ChatReactionFlightController? flightController;
        private readonly System.Random rng;
        private readonly int atlasTotalTiles;

        private RectTransform? defaultSpawnRect;

        public int AliveCount => uiStore.Count;
        public int PoolCapacity => uiStore.Capacity;
        public bool IsStreaming => streamEmitter.IsStreaming;

        public void SetDefaultSpawnRect(RectTransform rect) { defaultSpawnRect = rect; }

        public ChatReactionUISimulation(ChatReactionsConfig config, RectTransform laneRect)
        {
            this.config = config;
            rng = new System.Random();
            atlasTotalTiles = config.SafeTotalTiles;

            runtimeMaterial = ChatReactionMaterialFactory.CreateRuntimeMaterial(config);
            uiStore = new DenseParticleStore<ChatReactionsUiParticle>(config.UILane.MaxParticles);
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
            if (uiStore.Count > 0)
            {
                ApplyFlightSteering(dt);

                Profiler.BeginSample("ChatReactions.UI.Physics");
                ParticleIntegrator.Step(uiStore.Buffer, uiStore.Count,
                    config.UILane.Gravity, config.UILane.Drag, dt);
                uiStore.CompactDead();
                Profiler.EndSample();
            }

            EmitStreamParticles(dt);
        }

        public void Draw(Camera cam)
        {
            if (cam == null || uiStore.Count == 0) return;

            Profiler.BeginSample("ChatReactions.UI.Draw");
            renderer.Draw(cam, uiStore.Buffer, uiStore.Count, 0, config.UILane.DepthFromCamera);
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

        public void BeginUIStream(RectTransform sourceRect) =>
            streamEmitter.Begin(sourceRect);

        public void EndUIStream() =>
            streamEmitter.End();

        public void ToggleUIStream(RectTransform sourceRect) =>
            streamEmitter.Toggle(sourceRect);

        public void BeginDebugUIStream(RectTransform? sourceRect = null) => streamEmitter.Begin(sourceRect);
        public void EndDebugUIStream() => streamEmitter.End();

        private void ApplyFlightSteering(float dt)
        {
            if (flightController == null) return;

            Profiler.BeginSample("ChatReactions.UI.FlightSteer");
            var buffer = uiStore.Buffer;

            for (int i = 0; i < uiStore.Count; i++)
            {
                ref var p = ref buffer[i];

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
                int index = GetRandomEmojiIndex();
                RectTransform? source = streamEmitter.Source;

                if (source != null)
                    TriggerUIReactionFromRect(source, index, config.UILane.StreamBurst);
                else
                    TriggerUIReaction(index, config.UILane.StreamBurst);
            }

            Profiler.EndSample();
        }

        private void SpawnBurst(Vector2 basePx, int emojiIndex, int count, float jitterH, float jitterV)
        {
            var ui = config.UILane;
            int n = Mathf.Max(1, count);

            for (int i = 0; i < n; i++)
            {
                float endSizePx = rng.NextFloat(ui.SizeRange.x, ui.SizeRange.y);
                float startSizePx = endSizePx * rng.NextFloat(config.SpawnSizeMinRatio, config.SpawnSizeMaxRatio);
                float lifetime = rng.NextFloat(ui.LifetimeRange.x, ui.LifetimeRange.y);

                Vector2 velocity = ResolveSpawnVelocity(ui, out float phase);
                Vector2 jitter = new Vector2(rng.NextFloat(-jitterH, jitterH), rng.NextFloat(-jitterV, jitterV));

                uiStore.Add(new ChatReactionsUiParticle
                {
                    screenPos = basePx + jitter,
                    screenVel = velocity,
                    age = 0f,
                    lifetime = lifetime,
                    startSizePx = startSizePx,
                    endSizePx = endSizePx,
                    emojiIndex = emojiIndex,
                    zigZagPhase = phase,
                    alive = 1,
                });
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
            float speed = rng.NextFloat(ui.SpeedRange.x, ui.SpeedRange.y);
            return new Vector2(rng.NextFloat(-FALLBACK_LATERAL_RANGE, FALLBACK_LATERAL_RANGE), speed);
        }

        private Vector2 ResolveDefaultSpawnPosition() =>
            defaultSpawnRect != null
                ? spawnResolver.GetSpawnPxFromRectCenter(defaultSpawnRect)
                : spawnResolver.GetSpawnPxBottomCenter();

        public int GetRandomEmojiIndex() =>
            rng.Next(0, atlasTotalTiles);

    }
}
