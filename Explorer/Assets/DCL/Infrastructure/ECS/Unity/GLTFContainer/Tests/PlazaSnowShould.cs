using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class PlazaSnowShould
    {
        private const string SHOP_INTERIOR = "models/shop_interior.glb";
        private const string FOUNTAIN = "models/fountain.glb";

        private PlazaSnow plazaSnow = null!;
        private GltfContainerAsset asset = null!;
        private MeshRenderer renderer = null!;

        [SetUp]
        public void SetUp()
        {
            plazaSnow = new PlazaSnow(new PlazaSnowConfig
            {
                baseParcels = new[] { "0,0", " -9, 3 ", "invalid" },
                excludedGltfs = new[] { SHOP_INTERIOR },
            });

            var root = new GameObject();
            renderer = root.AddComponent<MeshRenderer>();
            asset = GltfContainerAsset.Create(root, assetData: null);
            asset.Renderers.Add(renderer);
        }

        [TearDown]
        public void TearDown()
        {
            asset.Dispose();
        }

        [Test]
        public void IncludeOnlyConfiguredBaseParcels()
        {
            // Assert
            Assert.IsTrue(plazaSnow.IncludesScene(new Vector2Int(0, 0)));
            Assert.IsTrue(plazaSnow.IncludesScene(new Vector2Int(-9, 3)));
            Assert.IsFalse(plazaSnow.IncludesScene(new Vector2Int(1, 0)));
        }

        [Test]
        public void TagRenderersInSnowScene()
        {
            // Act
            plazaSnow.TagRenderers(asset, FOUNTAIN, true);

            // Assert
            Assert.AreEqual(PlazaSnow.RENDERING_LAYER_MASK, renderer.renderingLayerMask & PlazaSnow.RENDERING_LAYER_MASK);
        }

        [Test]
        public void SkipExcludedGltfIgnoringCase()
        {
            // Act
            plazaSnow.TagRenderers(asset, SHOP_INTERIOR.ToUpperInvariant(), true);

            // Assert
            Assert.AreEqual(0u, renderer.renderingLayerMask & PlazaSnow.RENDERING_LAYER_MASK);
        }

        [Test]
        public void ClearTagWhenReusedOutsideSnowScene()
        {
            // Arrange
            plazaSnow.TagRenderers(asset, FOUNTAIN, true);

            // Act
            plazaSnow.TagRenderers(asset, FOUNTAIN, false);

            // Assert
            Assert.AreEqual(0u, renderer.renderingLayerMask & PlazaSnow.RENDERING_LAYER_MASK);
            Assert.AreNotEqual(0u, renderer.renderingLayerMask, "Other rendering layers must be kept");
        }
    }
}
