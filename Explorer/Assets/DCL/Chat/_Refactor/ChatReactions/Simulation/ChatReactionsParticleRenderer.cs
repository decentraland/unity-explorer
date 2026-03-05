using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Drives <c>Graphics.DrawMeshInstanced</c> for GPU-instanced emoji particle rendering.
    /// Batches up to 1023 particles per draw call (hard limit of DrawMeshInstanced).
    /// All per-frame properties are pushed through a <c>MaterialPropertyBlock</c> —
    /// the shared material asset is never mutated at runtime.
    /// </summary>
    public sealed class ChatReactionsParticleRenderer
    {
        private const int BATCH_SIZE = 1023;

        private readonly Mesh quad;
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

        /// <param name="material">Shared emoji instanced material.</param>
        /// <param name="sizeOverLifetime">Optional curve (normalised lifetime [0,1] → size multiplier).
        /// Used for the pop-on-death effect. Pass <c>null</c> to use raw particle sizes.</param>
        public ChatReactionsParticleRenderer(Material material, AnimationCurve? sizeOverLifetime = null)
        {
            mat = material;
            mpb = new MaterialPropertyBlock();
            quad = CreateQuadMesh();
            this.sizeOverLifetime = sizeOverLifetime;
        }

        /// <param name="cam">Camera to render to. Pass <c>null</c> to render to all cameras
        /// whose culling mask includes <paramref name="layer"/>.</param>
        public void Draw(Camera cam, ChatReactionsParticle[] particles, int layer, float globalAlpha = 1f)
        {
            if (mat == null) return;

            int batchCount = 0;

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].alive == 0) continue;

                ref var p = ref particles[i];

                float t = p.lifetime > 0f ? Mathf.Clamp01(p.age / p.lifetime) : 0f;
                float startSize = p.startSize;
                float endSize = p.endSize;

                if (sizeOverLifetime != null)
                {
                    float multiplier = sizeOverLifetime.Evaluate(t);
                    startSize *= multiplier;
                    endSize *= multiplier;
                }

                matrices[batchCount] = Matrix4x4.identity;
                posSize[batchCount] = new Vector4(p.pos.x, p.pos.y, p.pos.z, startSize);
                extra[batchCount] = new Vector4(endSize, 0f, 0f, 0f);
                emoji[batchCount] = new Vector4(p.emojiIndex, 0f, 0f, 0f);
                lifeT[batchCount] = new Vector4(t, 0f, 0f, 0f);

                batchCount++;

                if (batchCount == BATCH_SIZE)
                {
                    Flush(cam, layer, batchCount, globalAlpha);
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
                Flush(cam, layer, batchCount, globalAlpha);
        }

        private void Flush(Camera cam, int layer, int count, float globalAlpha)
        {
            mpb.Clear();
            mpb.SetFloat(GlobalAlphaId, globalAlpha);
            mpb.SetVectorArray(PosSizeId, posSize);
            mpb.SetVectorArray(ExtraId, extra);
            mpb.SetVectorArray(EmojiId, emoji);
            mpb.SetVectorArray(LifeTId, lifeT);

            Graphics.DrawMeshInstanced(quad, 0, mat, matrices, count, mpb,
                ShadowCastingMode.Off, false, layer, cam);
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

            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }
    }
}
