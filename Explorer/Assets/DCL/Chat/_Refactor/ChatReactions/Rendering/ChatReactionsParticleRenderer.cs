using DCL.Chat.ChatReactions.Simulation;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace DCL.Chat.ChatReactions.Rendering
{
    /// <summary>
    /// Drives <c>Graphics.RenderMeshInstanced</c> for GPU-instanced emoji particle rendering.
    /// Converts screen-space particle positions to world space each frame so particles
    /// stay fixed on screen when the camera moves. Batches up to 1023 particles per draw call.
    /// </summary>
    public sealed class ChatReactionsParticleRenderer
    {
        private const int BATCH_SIZE = 1023;
        private const int SIZE_LUT_RESOLUTION = 256;

        // SetVectorArray uploads array.Length entries regardless of active count.
        // Pre-allocated tiers let Flush pick the smallest array that fits,
        // reducing GPU upload bandwidth for partially-filled batches.
        private static readonly int[] TIER_SIZES = { 16, 64, 256, 512, BATCH_SIZE };

        private static Mesh? sharedQuad;

        private readonly Material mat;
        private readonly MaterialPropertyBlock mpb;
        private readonly float[]? sizeLut;

        private readonly Matrix4x4[] matrices = new Matrix4x4[BATCH_SIZE];
        private readonly Vector4[] posSize = new Vector4[BATCH_SIZE];
        private readonly Vector4[] packed = new Vector4[BATCH_SIZE];

        private readonly Vector4[][] tieredPosSize;
        private readonly Vector4[][] tieredPacked;

        // MPB locks array length on first SetVectorArray call.
        // Track the last tier to detect size changes that require mpb.Clear().
        private int lastTierIndex = -1;

        private static readonly int PosSizeId = Shader.PropertyToID("_PosSize");
        private static readonly int PackedId = Shader.PropertyToID("_Packed");
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
            this.sizeLut = MathUtils.BakeCurve(sizeOverLifetime, SIZE_LUT_RESOLUTION);

            tieredPosSize = new Vector4[TIER_SIZES.Length][];
            tieredPacked = new Vector4[TIER_SIZES.Length][];

            for (int i = 0; i < TIER_SIZES.Length - 1; i++)
            {
                tieredPosSize[i] = new Vector4[TIER_SIZES[i]];
                tieredPacked[i] = new Vector4[TIER_SIZES[i]];
            }

            // The last tier shares the working arrays — no copy needed for full batches.
            tieredPosSize[TIER_SIZES.Length - 1] = posSize;
            tieredPacked[TIER_SIZES.Length - 1] = packed;

            // RenderMeshInstanced uses identity matrices; the shader repositions vertices via _PosSize.
            for (int i = 0; i < BATCH_SIZE; i++)
                matrices[i] = Matrix4x4.identity;
        }

        /// <summary>
        /// Draws only the world-space particles at the given indices.
        /// Used with <see cref="ParticleVisibilityCuller"/> for camera-culled rendering.
        /// </summary>
        /// <param name="zigZagAmplitude">Peak lateral displacement (world units). Zero disables oscillation.</param>
        /// <param name="zigZagOmega">Angular frequency (rad/s) for the sinusoidal wobble.</param>
        public void Draw(ChatReactionsParticle[] buffer, int[] visibleIndices, int visibleCount, int layer,
            float globalAlpha = 1f, float zigZagAmplitude = 0f, float zigZagOmega = 0f)
        {
            if (mat == null || visibleCount == 0) return;

            bool hasZigZag = zigZagAmplitude > 0f;
            int batchCount = 0;

            for (int k = 0; k < visibleCount; k++)
            {
                ref readonly var p = ref buffer[visibleIndices[k]];
                float t = LifetimeT(p.age, p.lifetime);

                Vector3 renderPos = p.pos;

                if (hasZigZag)
                {
                    float offset = zigZagAmplitude * Mathf.Sin(p.age * zigZagOmega);
                    renderPos.x += Mathf.Cos(p.zigZagPhase) * offset;
                    renderPos.z += Mathf.Sin(p.zigZagPhase) * offset;
                }

                WriteBatchSlot(batchCount, renderPos,
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

            // Precalculate screen-to-world basis vectors. Since depthFromCamera is constant
            // for the entire batch, the mapping is linear: 3 native calls replace N.
            Vector3 screenOrigin = cam.ScreenToWorldPoint(new Vector3(0f, 0f, depthFromCamera));
            Vector3 worldPerPixelX = (cam.ScreenToWorldPoint(new Vector3(cam.pixelWidth, 0f, depthFromCamera)) - screenOrigin) / cam.pixelWidth;
            Vector3 worldPerPixelY = (cam.ScreenToWorldPoint(new Vector3(0f, cam.pixelHeight, depthFromCamera)) - screenOrigin) / cam.pixelHeight;

            int batchCount = 0;

            for (int i = 0; i < count; i++)
            {
                ref readonly var p = ref buffer[i];
                float t = LifetimeT(p.age, p.lifetime);

                Vector3 worldPos = screenOrigin + worldPerPixelX * p.screenPos.x + worldPerPixelY * p.screenPos.y;

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

        private float ApplySizeCurve(float size, float t)
        {
            if (sizeLut == null) return size;

            int lutIndex = Mathf.Min((int)(t * (SIZE_LUT_RESOLUTION - 1)), SIZE_LUT_RESOLUTION - 1);
            return size * sizeLut[lutIndex];
        }

        private static float WorldSizePerPixel(Camera cam, float depth) =>
            cam.orthographic
                ? (2f * cam.orthographicSize) / cam.pixelHeight
                : (2f * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f)) / cam.pixelHeight;

        private void WriteBatchSlot(int slot, Vector3 worldPos, float startSize, float endSize, int emojiIndex, float t)
        {
            posSize[slot] = new Vector4(worldPos.x, worldPos.y, worldPos.z, startSize);
            packed[slot] = new Vector4(endSize, emojiIndex, t, 0f);
        }

        private void Flush(int layer, int count, float globalAlpha)
        {
            Profiler.BeginSample("ChatReactions.Flush");

            // Pick the smallest tier that fits the active count.
            int tierIndex = 0;

            while (TIER_SIZES[tierIndex] < count)
                tierIndex++;

            Vector4[] uploadPosSize = tieredPosSize[tierIndex];
            Vector4[] uploadPacked = tieredPacked[tierIndex];

            // The last tier already points to the working arrays (posSize/packed),
            // so only smaller tiers need a copy of the active region.
            if (tierIndex < TIER_SIZES.Length - 1)
            {
                System.Array.Copy(posSize, 0, uploadPosSize, 0, count);
                System.Array.Copy(packed, 0, uploadPacked, 0, count);
            }

            // MPB locks the array length on first SetVectorArray call for each property.
            // Subsequent calls with a different-length array are silently truncated.
            // Clear the MPB when the tier changes to reset the length lock.
            if (tierIndex != lastTierIndex)
            {
                mpb.Clear();
                lastTierIndex = tierIndex;
            }

            mpb.SetFloat(GlobalAlphaId, globalAlpha);
            mpb.SetVectorArray(PosSizeId, uploadPosSize);
            mpb.SetVectorArray(PackedId, uploadPacked);

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
