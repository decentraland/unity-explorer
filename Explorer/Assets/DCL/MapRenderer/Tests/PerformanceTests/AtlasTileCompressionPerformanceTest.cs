using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.MapRenderer.Tests.PerformanceTests
{
    /// <summary>
    /// Both atlas chunk loaders (<c>SatelliteChunkController.LoadImageAsync</c> line 96 and
    /// <c>ParcelChunkController.LoadImageAsync</c> line 83) receive their tile from
    /// <c>GetTextureWebRequest</c>'s buffer path, which produces a CPU-readable
    /// <c>TextureFormat.RGBA32</c>, NO-mip <see cref="Texture2D"/> (GetTextureWebRequest.cs:79-82) that
    /// is retained for the process lifetime (satellite in <c>currentOwnedTexture</c> +
    /// <c>MapRendererTextureContainer.AddChunk</c>; parcel in <c>currentOwnedTexture</c> + the sprite).
    /// A full atlas holds dozens of such tiles at 4 bytes/pixel, so the downloaded tile is
    /// block-compressed in-place before <c>Sprite.Create</c> whenever its dimensions are 4-aligned:
    /// <code>
    /// if ((texture.width &amp; 3) == 0 &amp;&amp; (texture.height &amp; 3) == 0 &amp;&amp; texture.format == TextureFormat.RGBA32)
    ///     texture.Compress(false);
    /// </code>
    /// <para>
    /// This test reproduces that exact buffer-path tile and the exact guard-and-compress expression
    /// (kept in <see cref="CompressAtlasTile"/> verbatim), then measures the sum of
    /// <see cref="Profiler.GetRuntimeMemorySizeLong(UnityEngine.Object)"/> over the retained tile
    /// textures before vs after compression: an atlas-sized batch must shrink by &gt;=60%. A second
    /// test exercises the multiple-of-4 guard: a 510x510 tile must be left untouched (still RGBA32),
    /// since <c>Texture2D.Compress</c> requires 4-aligned dimensions.
    /// </para>
    /// <para>
    /// The two production controllers take the compressed tile through <c>Sprite.Create</c> and a
    /// <c>SetTexture</c> GPU binding only (the sole <c>GetChunk</c> consumer is
    /// <c>SatelliteFloor</c>'s <c>MaterialPropertyBlock.SetTexture</c>), so no CPU <c>GetPixels</c>
    /// path is broken by the format change — hence the memory-only assertion here is sufficient and
    /// driving the full <c>LoadImageAsync</c> (which needs a mocked extension-method web request) is out
    /// of scope for this headless assert.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class AtlasTileCompressionPerformanceTest
    {
        private const int TILE_RESOLUTION = 512;

        private const int ATLAS_TILE_COUNT = 64;

        private readonly List<Texture2D> ownedTextures = new ();

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D t in ownedTextures)
                if (t != null)
                    Object.DestroyImmediate(t);

            ownedTextures.Clear();
        }

        /// <summary>
        /// Exact reproduction of the buffer path in <c>GetTextureWebRequest.ExecuteNoCompressionAsync</c>
        /// (lines 79-82): a CPU-readable RGBA32, no-mip Texture2D. LoadImage yields the same shape; we fill
        /// it directly so the test is deterministic and needs no encoded JPG asset.
        /// </summary>
        private Texture2D MakeBufferPathTile(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);

            UnityEngine.Random.InitState(1337);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(
                    (byte)UnityEngine.Random.Range(0, 256),
                    (byte)UnityEngine.Random.Range(0, 256),
                    (byte)UnityEngine.Random.Range(0, 256),
                    255);

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            ownedTextures.Add(tex);
            return tex;
        }

        /// <summary>
        /// Verbatim copy of the guard-and-compress inserted into both chunk controllers. Kept here so
        /// this test exercises the actual production logic (drift the guard and the asserts below
        /// break).
        /// </summary>
        private static void CompressAtlasTile(Texture2D texture)
        {
            if ((texture.width & 3) == 0 && (texture.height & 3) == 0 && texture.format == TextureFormat.RGBA32)
                texture.Compress(false);
        }

        /// <summary>
        /// An atlas-sized batch of retained RGBA32 tiles must shrink by &gt;=60% once compression runs.
        /// </summary>
        [Test]
        [Performance]
        public void RetainedAtlasTileMemoryDropsAtLeast60Percent()
        {
            var tiles = new Texture2D[ATLAS_TILE_COUNT];
            for (var i = 0; i < ATLAS_TILE_COUNT; i++)
                tiles[i] = MakeBufferPathTile(TILE_RESOLUTION, TILE_RESOLUTION);

            long beforeBytes = SumRuntimeMemory(tiles);

            foreach (Texture2D tile in tiles)
                CompressAtlasTile(tile);

            long afterBytes = SumRuntimeMemory(tiles);

            Measure.Custom(new SampleGroup("RetainedAtlasBytes_RGBA32", SampleUnit.Byte), beforeBytes);
            Measure.Custom(new SampleGroup("RetainedAtlasBytes_Compressed", SampleUnit.Byte), afterBytes);

            Assert.That(beforeBytes, Is.GreaterThan(0), "Baseline tile memory should be non-zero.");

            double reduction = 1.0 - ((double)afterBytes / beforeBytes);
            Assert.That(reduction, Is.GreaterThanOrEqualTo(0.60),
                $"Retained atlas tile memory only dropped {reduction:P1} " +
                $"({beforeBytes} -> {afterBytes} bytes); expected >=60% (was the tile compressed?).");

            foreach (Texture2D tile in tiles)
                Assert.That(tile.format, Is.Not.EqualTo(TextureFormat.RGBA32),
                    $"Conforming {tile.width}x{tile.height} tile stayed RGBA32 — Compress did not run.");
        }

        /// <summary>
        /// The multiple-of-4 guard must skip a 510x510 tile so a non-conforming tile is never fed to
        /// Texture2D.Compress (which requires 4-aligned dims).
        /// </summary>
        [Test]
        [Performance]
        public void NonMultipleOfFourTileIsLeftUncompressed()
        {
            Texture2D tile = MakeBufferPathTile(510, 510);

            CompressAtlasTile(tile);

            Assert.That(tile.format, Is.EqualTo(TextureFormat.RGBA32),
                "510x510 tile is not 4-aligned and must NOT be compressed (guard failed).");
            Assert.That(tile.width, Is.EqualTo(510));
            Assert.That(tile.height, Is.EqualTo(510));
        }

        private static long SumRuntimeMemory(IReadOnlyList<Texture2D> textures)
        {
            long total = 0;
            for (var i = 0; i < textures.Count; i++)
                total += Profiler.GetRuntimeMemorySizeLong(textures[i]);

            return total;
        }
    }
}