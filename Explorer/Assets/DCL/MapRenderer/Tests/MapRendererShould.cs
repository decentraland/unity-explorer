using Cysharp.Threading.Tasks;
using DCLServices.MapRenderer.CommonBehavior;
using DCLServices.MapRenderer.ComponentsFactory;
using DCLServices.MapRenderer.Culling;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.Tests
{
    [TestFixture]
    public class MapRendererShould
    {
        private DCLServices.MapRenderer.MapRenderer mapRenderer;

        private Dictionary<MapLayer, IMapLayerController> layers;

        private static readonly MapLayer[] TEST_MAP_LAYERS =
        {
            MapLayer.PlayerMarker,
        };

        [SetUp]
        public async Task Setup()
        {
            var componentsFactory = Substitute.For<IMapRendererComponentsFactory>();

            componentsFactory.CreateAsync(Arg.Any<CancellationToken>())
                             .Returns(
                                  new UniTask<MapRendererComponents>(
                                      new MapRendererComponents(
                                          new GameObject("map_configuration_test").AddComponent<MapRendererConfiguration>(),
                                          EnumUtils.Values<MapLayer>().Where(l => l != MapLayer.None).ToDictionary(x => x, x => Substitute.For<IMapLayerController>()),
                                          new List<IZoomScalingLayer>(),
                                          Substitute.For<IMapCullingController>(),
                                          Substitute.For<IObjectPool<IMapCameraControllerInternal>>())));

            mapRenderer = new DCLServices.MapRenderer.MapRenderer(componentsFactory);
            await mapRenderer.InitializeAsync(CancellationToken.None);

            layers = mapRenderer.layersDictionary_Test;
        }

        [Test]
        public void InitializeLayers()
        {
            CollectionAssert.AreEquivalent(EnumUtils.Values<MapLayer>().Where(l => l != MapLayer.None), mapRenderer.initializedLayers_Test);
        }

        [Test]
        public void EnableLayerByMask([ValueSource(nameof(TEST_MAP_LAYERS))] MapLayer mask)
        {
            IMapActivityOwner owner = Substitute.For<IMapActivityOwner>();

            mapRenderer.EnableLayers_Test(owner, mask);

            foreach (MapLayer mapLayer in EnumUtils.Values<MapLayer>())
            {
                if (EnumUtils.HasFlag(mask, mapLayer))
                    layers[mapLayer].Received(1).Enable(Arg.Any<CancellationToken>());
            }
        }

        [Test]
        [Ignore("")]
        public void DisableLayerByMask([ValueSource(nameof(TEST_MAP_LAYERS))] MapLayer mask)
        {
            IMapActivityOwner owner = Substitute.For<IMapActivityOwner>();

            mapRenderer.EnableLayers_Test(owner, mask);
            mapRenderer.DisableLayers_Test(owner, mask);

            foreach (MapLayer mapLayer in EnumUtils.Values<MapLayer>())
            {
                if (EnumUtils.HasFlag(mask, mapLayer))
                    layers[mapLayer].Received(1).Disable(Arg.Any<CancellationToken>());
            }
        }

        [Test]
        [Ignore("")] 
        public void NotDisableLayerIfStillUsed([ValueSource(nameof(TEST_MAP_LAYERS))] MapLayer mask)
        {
            IMapActivityOwner owner = Substitute.For<IMapActivityOwner>();

            mapRenderer.EnableLayers_Test(owner, mask);
            mapRenderer.EnableLayers_Test(owner, mask);

            mapRenderer.DisableLayers_Test(owner, mask);

            foreach (MapLayer mapLayer in EnumUtils.Values<MapLayer>())
            {
                if (EnumUtils.HasFlag(mask, mapLayer))
                {
                    if (EnumUtils.HasFlag(mask, mapLayer))
                        layers[mapLayer].DidNotReceive().Disable(default);
                }
            }
        }
    }
}
