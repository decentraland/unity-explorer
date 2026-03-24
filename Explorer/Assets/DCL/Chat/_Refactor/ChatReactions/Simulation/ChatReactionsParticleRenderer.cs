using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Drives <c>Graphics.RenderMeshInstanced</c> for GPU-instanced emoji particle rendering.
    /// Converts screen-space particle positions to world space each frame so particles
    /// stay fixed on screen when the camera moves. Batches up to 1023 particles per draw call.
    /// </summary>
    public sealed class ChatReactionsParticleRenderer
    {
        private const int BATCH_SIZE = 1023;

        private static Mesh? sharedQuad;

        private readonly Material mat;
        private readonly MaterialPropertyBlock mpb;
        private readonly AnimationCurve? sizeOverLifetime;

        private readonly Matrix4x4[] matrices = new Matrix4x4[BATCH_SIZE];
        private readonly Vector4[] posSize = new Vector4[BATCH_SIZE];
        private readonly Vector4[] extra = new Vector4[BATCH_SIZE];
        private readonly Vector4[] emoji = new Vector4[BATCH_SIZE];
        private readonly Vector4[] lifeT = new Vector4[BATCH_SIZE];

        private static readonly int PosSizeId = Shader.PropertyToID("_PosSize");
        private static readonly int ExtraId = Shader.PropertyToID("_Extra");
        private static readonly int EmojiId = Shader.PropertyToID("_Emoji");
        private static readonly int LifeTId = Shader.PropertyToID("_LifeT");
        private static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");

        private static readonly Bounds WORLD_BOUNDS = new (Vector3.zero, Vector3.one * 10000f);

        /// <param name="material">Shared emoji instanced material.</param>
        /// <param name="sizeOverLifetime">Optional curve (normalised lifetime [0,1] → size multiplier).
        /// Used for the pop-on-death effect. Pass <c>null</c> to use raw particle sizes.</param>
        public ChatReactionsParticleRenderer(Material material, AnimationCurve? sizeOverLifetime = null)
        {
            mat = material;
            mpb = new MaterialPropertyBlock();
            sharedQuad ??= CreateQuadMesh();
            this.sizeOverLifetime = sizeOverLifetime;

            // RenderMeshInstanced uses identity matrices; the shader repositions vertices via _PosSize.
            for (int i = 0; i < BATCH_SIZE; i++)
                matrices[i] = Matrix4x4.identity;
        }

        /// <summary>
        /// Draws only the world-space particles at the given indices.
        /// Used with <see cref="ParticleVisibilityCuller"/> for camera-culled rendering.
        /// </summary>
        public void Draw(ChatReactionsParticle[] buffer, int[] visibleIndices, int visibleCount, int layer, float globalAlpha = 1f)
        {
            if (mat == null || visibleCount == 0) return;

            int batchCount = 0;

            for (int k = 0; k < visibleCount; k++)
            {
                ref readonly var p = ref buffer[visibleIndices[k]];
                float t = LifetimeT(p.age, p.lifetime);

                WriteBatchSlot(batchCount, p.pos,
                    ApplySizeCurve(p.startSize, t),
                    ApplySizeCurve(p.endSize, t),
                    p.emojiIndex, t);

                batchCount++;

                if (batchCount == BATCH_SIZE)
                {
                    Flush(layer, batchCount, globalAlpha);
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
                Flush(layer, batchCount, globalAlpha);
        }

        /// <summary>
        /// Draws screen-space particles using a count bound (dense array — no alive checks).
        /// Converts pixel coordinates to world space via the camera.
        /// </summary>
        public void Draw(Camera cam, ChatReactionsUiParticle[] buffer, int count, int layer, float depthFromCamera, float globalAlpha = 1f)
        {
            if (mat == null || cam == null || count == 0) return;

            float worldSizePerPixel = WorldSizePerPixel(cam, depthFromCamera);

            if (worldSizePerPixel <= 0f || cam.pixelHeight <= 0)
                return;

            int batchCount = 0;

            for (int i = 0; i < count; i++)
            {
                ref readonly var p = ref buffer[i];
                float t = LifetimeT(p.age, p.lifetime);

                Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(p.screenPos.x, p.screenPos.y, depthFromCamera));

                WriteBatchSlot(batchCount, worldPos,
                    ApplySizeCurve(p.startSizePx, t) * worldSizePerPixel,
                    ApplySizeCurve(p.endSizePx, t) * worldSizePerPixel,
                    p.emojiIndex, t);

                batchCount++;

                if (batchCount == BATCH_SIZE)
                {
                    Flush(layer, batchCount, globalAlpha);
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
                Flush(layer, batchCount, globalAlpha);
        }

        private static float LifetimeT(float age, float lifetime) =>
            lifetime > 0f ? Mathf.Clamp01(age / lifetime) : 0f;

        private float ApplySizeCurve(float size, float t) =>
            sizeOverLifetime != null ? size * sizeOverLifetime.Evaluate(t) : size;

        private static float WorldSizePerPixel(Camera cam, float depth) =>
            cam.orthographic
                ? (2f * cam.orthographicSize) / cam.pixelHeight
                : (2f * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f)) / cam.pixelHeight;

        private void WriteBatchSlot(int slot, Vector3 worldPos, float startSize, float endSize, int emojiIndex, float t)
        {
            posSize[slot] = new Vector4(worldPos.x, worldPos.y, worldPos.z, startSize);
            extra[slot] = new Vector4(endSize, 0f, 0f, 0f);
            emoji[slot] = new Vector4(emojiIndex, 0f, 0f, 0f);
            lifeT[slot] = new Vector4(t, 0f, 0f, 0f);
        }

        private void Flush(int layer, int count, float globalAlpha)
        { 
            Profiler.BeginSample("ChatReactions.Flush");
            mpb.SetFloat(GlobalAlphaId, globalAlpha);
            mpb.SetVectorArray(PosSizeId, posSize);
            mpb.SetVectorArray(ExtraId, extra);
            mpb.SetVectorArray(EmojiId, emoji);
            mpb.SetVectorArray(LifeTId, lifeT);

            var renderParams = new RenderParams(mat)
            {
                layer = layer,
                matProps = mpb,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = WORLD_BOUNDS,
            };

            Graphics.RenderMeshInstanced(renderParams, sharedQuad, 0, matrices, count);
            Profiler.EndSample();
        }

        private static Mesh CreateQuadMesh()
        {
            var m = new Mesh { name = "EmojiQuad" };

            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };

            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };

            // Oversized bounds prevent frustum culling — the shader repositions vertices via _PosSize instance data
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            m.UploadMeshData(true);
            return m;
        }
    }
}
