using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Pure C# simulation for the situational reaction particle system.
    /// Particles live in screen space (pixels) so they do not move with the camera.
    /// Owns the particle pool, GPU renderer, spawn resolver, and stream emitter.
    /// </summary>
    public sealed class ChatReactionSimulation : IDisposable
    {
        private static readonly int AtlasTexId = Shader.PropertyToID("_AtlasTex");
        private static readonly int AtlasColsId = Shader.PropertyToID("_AtlasCols");
        private static readonly int AtlasRowsId = Shader.PropertyToID("_AtlasRows");
        private static readonly int FlipYId = Shader.PropertyToID("_FlipY");

        private readonly ChatReactionsSituationalConfig config;
        private readonly UiReactionParticlePool uiPool;
        private readonly ChatReactionsParticleRenderer renderer;
        private readonly UIReactionSpawnResolver spawnResolver;
        private readonly UIReactionStreamEmitter streamEmitter;
        private readonly ChatReactionFlightController? flightController;
        private readonly System.Random rng;

        public ChatReactionSimulation(ChatReactionsSituationalConfig config, RectTransform laneRect)
        {
            this.config = config;

            ApplyAtlasToMaterial(config);

            rng = new System.Random();
            uiPool = new UiReactionParticlePool(config.UILane.MaxParticles);
            spawnResolver = new UIReactionSpawnResolver(laneRect);
            streamEmitter = new UIReactionStreamEmitter();

            if (config.UILane.FlightPath != null)
                flightController = new ChatReactionFlightController(config.UILane.FlightPath, rng);

            renderer = new ChatReactionsParticleRenderer(
                config.EmojiMaterial,
                config.UILane.FlightPath?.SizeOverLifetime);
        }

        public void Dispose() { }

        public void Tick(float dt)
        {
            uiPool.Update(dt, config.UILane.Gravity, config.UILane.Drag);
            TickFlightSteering(dt);
            ClampLiveParticlesToLane();
            TickStream(dt);
        }

        public void Draw(Camera cam)
        {
            if (cam == null) return;

            renderer.Draw(cam, uiPool.Raw, config.UILane.RenderLayer, config.UILane.DepthFromCamera);
        }

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            if (!spawnResolver.TryGetSpawnPxBottomCenter(out Vector2 basePx)) return;

            SpawnBurst(basePx, emojiIndex, count, jitterH: 12f, jitterV: 8f);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            if (!spawnResolver.TryGetSpawnPxFromRectCenter(sourceRect, out Vector2 basePx)) return;

            SpawnBurst(basePx, emojiIndex, count, jitterH: 8f, jitterV: 4f);
        }

        public void TriggerDefaultUIReaction()
        {
            int index = config.UILane.RandomEmoji
                ? rng.Next(0, GetAtlasTotalTiles())
                : config.UILane.DefaultEmojiIndex;

            TriggerUIReaction(index, config.UILane.StreamBurst);
        }

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect)
        {
            int index = config.UILane.RandomEmoji
                ? rng.Next(0, GetAtlasTotalTiles())
                : config.UILane.DefaultEmojiIndex;

            TriggerUIReactionFromRect(sourceRect, index, config.UILane.StreamBurst);
        }

        public void BeginUIStream(RectTransform sourceRect) =>
            streamEmitter.Begin(sourceRect);

        public void EndUIStream() =>
            streamEmitter.End();

        public void ToggleUIStream(RectTransform sourceRect) =>
            streamEmitter.Toggle(sourceRect);

        private void ClampLiveParticlesToLane()
        {
            var particles = uiPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].alive == 0) continue;
                spawnResolver.ClampToLane(ref particles[i].screenPos);
            }
        }

        private void TickStream(float dt)
        {
            int ticks = streamEmitter.Tick(dt, config.UILane.StreamRatePerSecond);

            for (int t = 0; t < ticks; t++)
            {
                int index = config.UILane.RandomEmoji
                    ? rng.Next(0, GetAtlasTotalTiles())
                    : config.UILane.DefaultEmojiIndex;

                TriggerUIReactionFromRect(streamEmitter.Source!, index, config.UILane.StreamBurst);
            }
        }

        private void TickFlightSteering(float dt)
        {
            if (flightController == null) return;

            var particles = uiPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                float normalizedAge = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                Vector2 accel2D = flightController.GetSteering2D(normalizedAge);
                p.screenVel += accel2D * dt;
            }
        }

        private void SpawnBurst(Vector2 basePx, int emojiIndex, int count, float jitterH, float jitterV)
        {
            var ui = config.UILane;

            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float endSizePx = Rand(ui.SizeRange.x, ui.SizeRange.y);
                float startSizePx = endSizePx * Rand(0.2f, 0.5f);
                float lifetime = Rand(ui.LifetimeRange.x, ui.LifetimeRange.y);
                float baseSpeedPx = Rand(ui.SpeedRange.x, ui.SpeedRange.y);

                Vector2 velPx;

                if (flightController != null)
                {
                    velPx = flightController.GetSpawnVelocity2D(baseSpeedPx);
                }
                else
                {
                    velPx = new Vector2(Rand(-20f, 20f), baseSpeedPx);
                }

                Vector2 jitter = new Vector2(Rand(-jitterH, jitterH), Rand(-jitterV, jitterV));

                uiPool.Spawn(basePx + jitter, velPx, lifetime, startSizePx, endSizePx, emojiIndex);
            }
        }

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));

        private int GetAtlasTotalTiles() =>
            config.Atlas != null ? Mathf.Max(1, config.Atlas.TotalTiles) : 1;

        private static void ApplyAtlasToMaterial(ChatReactionsSituationalConfig config)
        {
            if (config.EmojiMaterial == null || config.Atlas == null) return;

            config.EmojiMaterial.SetTexture(AtlasTexId, config.Atlas.Atlas);
            config.EmojiMaterial.SetFloat(AtlasColsId, config.Atlas.Cols);
            config.EmojiMaterial.SetFloat(AtlasRowsId, config.Atlas.Rows);
            config.EmojiMaterial.SetFloat(FlipYId, config.Atlas.FlipY ? 1f : 0f);
        }
    }
}
