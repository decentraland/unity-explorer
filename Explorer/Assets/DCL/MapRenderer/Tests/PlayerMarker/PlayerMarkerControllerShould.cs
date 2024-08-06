using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.MapRenderer.Tests.PlayerMarker
{
    [TestFixture]
    public class PlayerMarkerControllerShould
    {
        [SetUp]
        public void Setup()
        {
            ICoordsUtils coordUtils = Substitute.For<ICoordsUtils>();

            coordUtils.CoordsToPositionWithOffset(Arg.Any<Vector2>())
                      .Returns(info => (Vector3)info.Arg<Vector2>());

            builder = Substitute.For<PlayerMarkerController.PlayerMarkerBuilder>();
            builder.Invoke(Arg.Any<Transform>()).Returns(_ => marker = Substitute.For<IPlayerMarker>());

            controller = new PlayerMarkerController(
                builder,
                null,
                coordUtils,
                Substitute.For<IMapCullingController>(),
                Substitute.For<IMapPathEventBus>()
            );

            controller.Initialize();
        }

        private PlayerMarkerController controller;
        private IPlayerMarker marker;
        private PlayerMarkerController.PlayerMarkerBuilder builder;

        [Test]
        public void Initialize()
        {
            builder.Received(1).Invoke(Arg.Any<Transform>());
        }

        [Test]
        public async Task SetActiveOnEnable()
        {
            await controller.Enable(CancellationToken.None);
            marker.Received(1).SetActive(true);
        }

        [Test]
        public void Dispose()
        {
            controller.Dispose();
            marker.Received(1).Dispose();
        }

        [Test]
        public async Task DeactivateOnDisable()
        {
            await controller.Enable(CancellationToken.None);
            await controller.Disable(CancellationToken.None);

            marker.Received().SetActive(false);
        }
    }
}
