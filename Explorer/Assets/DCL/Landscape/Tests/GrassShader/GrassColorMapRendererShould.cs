using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StylizedGrass.Tests
{
    [TestFixture]
    public class GrassColorMapRendererShould
    {
        private static readonly int COLOR_MAP_ID = Shader.PropertyToID("_ColorMap");
        private static readonly int COLOR_MAP_BOUNDS_ID = Shader.PropertyToID("_ColorMapBounds");
        private static readonly int COLOR_MAP_TEXEL_SIZE_ID = Shader.PropertyToID("_ColorMap_TexelSize");

        private GameObject host;
        private GrassColorMapRenderer renderer;
        private GrassColorMap colorMap;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GrassColorMapRenderer");
            renderer = host.AddComponent<GrassColorMapRenderer>();
            colorMap = ScriptableObject.CreateInstance<GrassColorMap>();
            renderer.colorMap = colorMap;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(colorMap);
        }

        [Test]
        public void EncapsulateSplatSourcesWhenRecalculatingBounds()
        {
            // Arrange
            renderer.splatSources.Add(Source(new Vector3(-32f, 0f, -16f), new Vector3(64f, 0f, 48f)));
            renderer.splatSources.Add(Source(new Vector3(0f, 0f, 0f), new Vector3(96f, 0f, 8f)));

            // Act
            renderer.RecalculateBounds();

            // Assert
            Assert.AreEqual(new Vector4(-32f, -16f, 128f, 48f), colorMap.BoundsVector);
        }

        [Test]
        public void IgnoreSplatSourcesWithoutAreaWhenRecalculatingBounds()
        {
            // Arrange
            renderer.splatSources.Add(Source(new Vector3(-32f, 0f, -16f), new Vector3(64f, 0f, 48f)));
            renderer.splatSources.Add(Source(new Vector3(500f, 0f, 500f), Vector3.zero));
            renderer.splatSources.Add(null);

            // Act
            renderer.RecalculateBounds();

            // Assert
            Assert.AreEqual(new Vector4(-32f, -16f, 64f, 48f), colorMap.BoundsVector);
        }

        [Test]
        public void EncapsulateTerrainObjectsTogetherWithSplatSources()
        {
            // Arrange
            var terrainData = new TerrainData { size = new Vector3(16f, 4f, 16f) };
            GameObject terrain = Terrain.CreateTerrainGameObject(terrainData);
            terrain.transform.position = new Vector3(100f, 0f, 100f);
            renderer.terrainObjects.Add(terrain);
            renderer.splatSources.Add(Source(new Vector3(-32f, 0f, -16f), new Vector3(64f, 0f, 48f)));

            try
            {
                // Act
                renderer.RecalculateBounds();

                // Assert
                Assert.AreEqual(new Vector4(-32f, -16f, 148f, 132f), colorMap.BoundsVector);
            }
            finally
            {
                Object.DestroyImmediate(terrain);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void PublishUntintedMapWhenThereIsNothingToBake()
        {
            // Arrange
            colorMap.bounds = default;

            // Act
            renderer.Render();

            // Assert
            Assert.AreSame(Texture2D.whiteTexture, Shader.GetGlobalTexture(COLOR_MAP_ID));
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(COLOR_MAP_BOUNDS_ID));
            Assert.IsTrue(colorMap.IsActive);
        }

        [Test]
        public void MapRegionIntoMapPixels()
        {
            // Arrange
            var region = new Bounds(new Vector3(0f, 0f, 8f), new Vector3(64f, 0f, 48f));
            var mapMin = new Vector2(-32f, -16f);
            var mapSize = new Vector2(128f, 96f);

            // Act
            bool found = GrassColorMapRenderer.TryGetViewport(region, mapMin, mapSize, 256, out Rect viewport);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new Rect(0f, 0f, 128f, 128f), viewport);
        }

        [Test]
        public void RejectRegionsWithoutArea()
        {
            // Act
            bool found = GrassColorMapRenderer.TryGetViewport(new Bounds(Vector3.zero, new Vector3(64f, 10f, 0f)),
                new Vector2(-32f, -16f), new Vector2(128f, 96f), 256, out Rect viewport);

            // Assert
            Assert.IsFalse(found);
            Assert.AreEqual(default(Rect), viewport);
        }

        [Test]
        public void ResolveControlThatSpansTheRegionToIdentity()
        {
            // Arrange
            var region = new Bounds(new Vector3(0f, 0f, 8f), new Vector3(64f, 0f, 48f));

            // Act
            Vector4 st = GrassColorMapRenderer.ControlST(region, new Vector2(64f, 48f), new Vector2(32f, 16f));

            // Assert
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), st);
        }

        [Test]
        public void ResolveTiledControlAcrossTheRegion()
        {
            // Arrange: a 1024 m tile whose origin sits 4096 m before the world origin, over a
            // region running from -2048 to 2048 on both axes.
            var region = new Bounds(Vector3.zero, new Vector3(4096f, 0f, 4096f));

            // Act
            Vector4 st = GrassColorMapRenderer.ControlST(region, new Vector2(1024f, 1024f), new Vector2(4096f, 4096f));

            // Assert
            Assert.AreEqual(new Vector4(4f, 4f, 2f, 2f), st);
        }

        [Test]
        public void PackLayerTilingLikeUnityTerrainLayers()
        {
            // Act
            Vector4 st = GrassColorMapRenderer.LayerST(new Vector2(4f, 8f), new Vector2(2f, 4f));
            Vector4 fallback = GrassColorMapRenderer.LayerST(Vector2.zero, new Vector2(3f, 5f));

            // Assert
            Assert.AreEqual(new Vector4(0.25f, 0.125f, 0.5f, 0.5f), st);
            Assert.AreEqual(new Vector4(1f, 1f, 3f, 5f), fallback);
        }

        [Test]
        public void MaskOnlyTheControlChannelsThatCarryALayer()
        {
            Assert.AreEqual(Vector4.zero, GrassColorMapRenderer.LayerMask(0));
            Assert.AreEqual(new Vector4(1f, 0f, 0f, 0f), GrassColorMapRenderer.LayerMask(1));
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), GrassColorMapRenderer.LayerMask(2));
            Assert.AreEqual(Vector4.one, GrassColorMapRenderer.LayerMask(4));
            Assert.AreEqual(Vector4.one, GrassColorMapRenderer.LayerMask(9));
        }

        [Test]
        public void ClampLayerCountToTheControlChannels()
        {
            // Arrange
            GrassColorMapSplatSource source = Source(Vector3.zero, new Vector3(1f, 0f, 1f));
            source.layers = new GrassColorMapSplatSource.Layer[6];

            // Assert
            Assert.AreEqual(GrassColorMapSplatSource.MAX_LAYERS, source.LayerCount);
        }

        [Test]
        public void PublishBoundsAndTexelSizeOfTheActiveMap()
        {
            // Arrange
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            colorMap.texture = texture;
            colorMap.bounds = new Bounds(new Vector3(8f, 0f, -8f), new Vector3(16f, 0f, 32f));

            try
            {
                // Act
                colorMap.SetActive();

                // Assert
                Assert.AreSame(texture, Shader.GetGlobalTexture(COLOR_MAP_ID));
                Assert.AreEqual(new Vector4(0f, -24f, 16f, 32f), Shader.GetGlobalVector(COLOR_MAP_BOUNDS_ID));
                Assert.AreEqual(new Vector4(0.25f, 0.5f, 4f, 2f), Shader.GetGlobalVector(COLOR_MAP_TEXEL_SIZE_ID));
            }
            finally
            {
                colorMap.texture = null;
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TrackOnlyTheLastActivatedMap()
        {
            // Arrange
            GrassColorMap other = ScriptableObject.CreateInstance<GrassColorMap>();

            try
            {
                // Act
                colorMap.SetActive();
                other.SetActive();

                // Assert
                Assert.IsFalse(colorMap.IsActive);
                Assert.IsTrue(other.IsActive);
                Assert.AreSame(other, GrassColorMap.Active);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        private static GrassColorMapSplatSource Source(Vector3 min, Vector3 size)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, min + size);

            return new GrassColorMapSplatSource { bounds = bounds };
        }
    }
}
