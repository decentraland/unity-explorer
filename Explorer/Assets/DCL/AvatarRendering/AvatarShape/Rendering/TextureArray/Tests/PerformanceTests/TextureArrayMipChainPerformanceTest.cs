using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests.PerformanceTests
{
    /// <summary>
    /// Falsifies fix #13 — "Restore the avatar Texture2DArray mip chain (both sites)".
    /// <para>
    /// The avatar toon <see cref="TextureArrayContainer"/> streams every wearable texture into a
    /// pooled <see cref="Texture2DArray"/> via <c>Graphics.CopyTexture</c>. Before the fix the arrays
    /// were allocated with <c>mipChain:false</c> (<see cref="TextureArraySlotHandler"/>) and the
    /// per-mip copy loop in <c>TextureArrayHandler.SetTexture</c> was commented out, so distant crowd
    /// avatars had no prefiltered mip to sample and <c>anisoLevel</c> was inert.
    /// </para>
    /// <para>
    /// Primary metric (per the fix spec's perf_test): <c>Texture2DArray.mipmapCount</c> read on EVERY
    /// avatar array produced for a 100-avatar crowd — it MUST be &gt; 1, and equal to the full chain
    /// for the 512px resolution. With the fix reverted (either site) the arrays report mipmapCount == 1
    /// and this test fails. The source textures below deliberately satisfy the fix's SOURCE-MIP
    /// GUARANTEE (created with a full mip chain) so the restored copy loop can fill every level;
    /// with a mip0-only source the per-mip copy would leave undefined lower mips — that path is a
    /// GPU-capture / RenderDoc concern, out of scope for this headless assert.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class TextureArrayMipChainPerformanceTest
    {
        private const string TOON_SHADER = "DCL/DCL_Toon";
        private const int RESOLUTION = TextureArrayConstants.MAIN_TEXTURE_RESOLUTION; // 512
        private const int CROWD_SIZE = 100;

        private static readonly int[] DEFAULT_RESOLUTIONS = { RESOLUTION };

        private TextureArrayContainer? container;
        private Material? sourceMaterial;
        private readonly List<Material> targetMaterials = new ();
        private readonly List<Texture> ownedTextures = new ();
        private TextureArraySlot?[]? perfSlots;
        private Material? perfTarget;

        // Full mip chain for a square texture: floor(log2(size)) + 1.
        private static int ExpectedMipCount(int size)
        {
            var mips = 1;
            while (size > 1)
            {
                size >>= 1;
                mips++;
            }

            return mips;
        }

        private Texture2D MakeMippedSource(TextureFormat format)
        {
            // mipChain:true is the fix's SOURCE-MIP GUARANTEE — the per-mip CopyTexture in SetTexture
            // requires matching source levels to fill the destination array's mips.
            var tex = new Texture2D(RESOLUTION, RESOLUTION, format, mipChain: true, linear: false);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            ownedTextures.Add(tex);
            return tex;
        }

        [SetUp]
        public void SetUp()
        {
            Shader targetShader = Shader.Find(TOON_SHADER);

            Texture bc7 = MakeMippedSource(TextureArrayConstants.DEFAULT_BASEMAP_TEXTURE_FORMAT);
            Texture bc5 = MakeMippedSource(TextureArrayConstants.DEFAULT_NORMALMAP_TEXTURE_FORMAT);

            var defaultTextures = new Dictionary<TextureArrayKey, Texture>
            {
                [new TextureArrayKey(TextureArrayConstants.MAINTEX_ARR_TEX_SHADER, RESOLUTION)] = bc7,
                [new TextureArrayKey(TextureArrayConstants.BASE_MAP_TEX_ARR, RESOLUTION)] = bc7,
                [new TextureArrayKey(TextureArrayConstants.NORMAL_MAP_TEX_ARR, RESOLUTION)] = bc5,
                [new TextureArrayKey(TextureArrayConstants.EMISSIVE_MAP_TEX_ARR, RESOLUTION)] = bc7,
            };

            container = new TextureArrayContainerFactory(defaultTextures).Create(targetShader, DEFAULT_RESOLUTIONS);

            sourceMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));
            foreach (TextureArrayMapping mapping in container.mappings)
            {
                Texture src = mapping.Handler.GetTextureFormat() == TextureFormat.BC7 ? bc7 : bc5;
                sourceMaterial.SetTexture(mapping.OriginalTextureID, src);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Material m in targetMaterials)
                if (m != null) Object.DestroyImmediate(m);
            targetMaterials.Clear();

            if (sourceMaterial != null) Object.DestroyImmediate(sourceMaterial);
            if (perfTarget != null) Object.DestroyImmediate(perfTarget);

            foreach (Texture t in ownedTextures)
                if (t != null) Object.DestroyImmediate(t);
            ownedTextures.Clear();

            container = null;
            sourceMaterial = null;
            perfSlots = null;
            perfTarget = null;
        }

        /// <summary>
        /// Uploads a <see cref="CROWD_SIZE"/>-avatar crowd, then asserts every distinct avatar
        /// Texture2DArray carries a real mip chain. This is the falsifier: mipChain:false (fix reverted)
        /// yields mipmapCount == 1 and fails the assert.
        /// </summary>
        [Test]
        [Performance]
        public void AllAvatarArraysHaveAFullMipChain()
        {
            int expectedMips = ExpectedMipCount(RESOLUTION);
            var arraysSeen = new HashSet<Texture2DArray>();

            for (var a = 0; a < CROWD_SIZE; a++)
            {
                var target = new Material(Shader.Find(TOON_SHADER));
                targetMaterials.Add(target);

                TextureArraySlot?[] slots = container!.SetTexturesFromOriginalMaterial(sourceMaterial!, target);
                foreach (TextureArraySlot? slot in slots)
                    if (slot.HasValue)
                        arraysSeen.Add(slot.Value.TextureArray);
            }

            Assert.That(arraysSeen.Count, Is.GreaterThan(0), "No avatar texture arrays were produced.");

            var minMipCount = int.MaxValue;
            foreach (Texture2DArray arr in arraysSeen)
            {
                Assert.That(arr.mipmapCount, Is.GreaterThan(1),
                    $"Avatar Texture2DArray '{arr.name}' has no mip chain (mipmapCount={arr.mipmapCount}); " +
                    "distant crowd avatars sample full-res mip0 every frame and anisoLevel is inert.");
                Assert.That(arr.mipmapCount, Is.EqualTo(expectedMips),
                    $"Avatar Texture2DArray '{arr.name}' has a truncated mip chain " +
                    $"(mipmapCount={arr.mipmapCount}, expected {expectedMips}).");
                if (arr.mipmapCount < minMipCount)
                    minMipCount = arr.mipmapCount;
            }

            Measure.Custom(new SampleGroup("MinAvatarArrayMipCount", SampleUnit.Undefined), minMipCount);
        }

        /// <summary>
        /// Measures the cost of the restored per-mip <c>CopyTexture</c> upload for a single avatar,
        /// freeing the slots each iteration so array growth stays bounded. Provides a regression signal
        /// on upload time now that the full mip chain (not just mip0) is copied.
        /// </summary>
        [Test]
        [Performance]
        public void MeasureSingleAvatarMippedUpload()
        {
            perfTarget = new Material(Shader.Find(TOON_SHADER));

            Measure
               .Method(() => { perfSlots = container!.SetTexturesFromOriginalMaterial(sourceMaterial!, perfTarget); })
               .WarmupCount(5)
               .MeasurementCount(30)
               .CleanUp(() =>
                {
                    if (perfSlots != null)
                    {
                        // Return the texture slots to their handlers' free stacks so GetNextFreeSlot
                        // recycles them each iteration — keeps array allocation bounded across the run.
                        foreach (TextureArraySlot? slot in perfSlots)
                            slot?.FreeSlot();

                        container!.ReleaseSlots(perfSlots);
                    }

                    perfSlots = null;
                })
               .Run();
        }
    }
}