using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Pure C# simulation for the situational reaction particle system.
    /// Owns the particle pool, GPU renderer, spawn resolver, and stream emitter.
    /// No Unity lifecycle — driven explicitly by <see cref="DCL.Chat.Reactions.SituationalReactionController"/>.
    /// </summary>
    public sealed class ChatReactionSimulation : IDisposable
    {
        private static readonly int AtlasTexId = Shader.PropertyToID("_AtlasTex");
        private static readonly int AtlasColsId = Shader.PropertyToID("_AtlasCols");
        private static readonly int AtlasRowsId = Shader.PropertyToID("_AtlasRows");
        private static readonly int FlipYId = Shader.PropertyToID("_FlipY");

        private readonly ChatReactionsSituationalConfig config;
        private readonly ChatReactionsParticlePool uiPool;
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
            uiPool = new ChatReactionsParticlePool(config.UILane.MaxParticles);
            spawnResolver = new UIReactionSpawnResolver(laneRect, config.UILane.DepthFromCamera);
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
            TickFlightSteering(dt);
            uiPool.Tick(dt, config.UILane.Gravity, config.UILane.Drag);
            ClampLiveParticlesToLane();
            TickStream(dt);
        }

        public void Draw()
        {
            renderer.Draw(null, uiPool.Raw, config.UILane.RenderLayer);
        }

        public void TriggerUIReaction(int emojiIndex, int count)
        {
            if (!spawnResolver.TryGetSpawnPosBottomCenter(out Vector3 basePos)) return;

            SpawnBurst(basePos, emojiIndex, count, jitterH: 0.05f, jitterV: 0.03f);
        }

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count)
        {
            if (!spawnResolver.TryGetSpawnPosFromRectCenter(sourceRect, out Vector3 basePos)) return;

            SpawnBurst(basePos, emojiIndex, count, jitterH: 0.02f, jitterV: 0.01f);
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
                spawnResolver.ClampToLane(ref particles[i].pos);
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

            spawnResolver.TryGetCameraAxes(out Vector3 right, out Vector3 up);
            var particles = uiPool.Raw;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;

                float normalizedAge = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                Vector2 accel2D = flightController.GetSteering2D(normalizedAge);
                p.vel += (right * accel2D.x + up * accel2D.y) * dt;
            }
        }

        private void SpawnBurst(Vector3 basePos, int emojiIndex, int count, float jitterH, float jitterV)
        {
            spawnResolver.TryGetCameraAxes(out Vector3 right, out Vector3 up);
            var ui = config.UILane;

            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float endSize = Rand(ui.SizeRange.x, ui.SizeRange.y);
                float startSize = endSize * Rand(0.2f, 0.5f);
                float lifetime = Rand(ui.LifetimeRange.x, ui.LifetimeRange.y);
                float baseSpeed = Rand(ui.SpeedRange.x, ui.SpeedRange.y);

                Vector3 vel;

                if (flightController != null)
                {
                    Vector2 vel2D = flightController.GetSpawnVelocity2D(baseSpeed);
                    vel = right * vel2D.x + up * vel2D.y;
                }
                else
                {
                    vel = FallbackUpwards(baseSpeed);
                }

                Vector3 jitter = right * Rand(-jitterH, jitterH) + up * Rand(-jitterV, jitterV);

                uiPool.Spawn(basePos + jitter, vel, lifetime, startSize, endSize, emojiIndex);
            }
        }

        private Vector3 FallbackUpwards(float speed)
        {
            var dir = new Vector3(Rand(-0.35f, 0.35f), 1f, Rand(-0.35f, 0.35f));
            return dir.normalized * speed;
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
