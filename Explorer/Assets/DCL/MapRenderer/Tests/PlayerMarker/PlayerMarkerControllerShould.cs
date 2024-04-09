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

    public class PlayerMarkerControllerShould
    {
        private PlayerMarkerController controller;
        private IPlayerMarker marker;
        private PlayerMarkerController.PlayerMarkerBuilder builder;


        public void Setup()
        {
            var coordUtils = Substitute.For<ICoordsUtils>();

            coordUtils.CoordsToPositionWithOffset(Arg.Any<Vector2>())
                      .Returns(info => (Vector3)info.Arg<Vector2>());

            builder = Substitute.For<PlayerMarkerController.PlayerMarkerBuilder>();
            builder.Invoke(Arg.Any<Transform>()).Returns(_ => marker = Substitute.For<IPlayerMarker>());

            controller = new PlayerMarkerController(
                builder,
                null,
                coordUtils,
                Substitute.For<IMapCullingController>()
            );

            controller.Initialize();
        }


        public void Initialize()
        {
            builder.Received(1).Invoke(Arg.Any<Transform>());
        }


        public async Task SetActiveOnEnable()
        {
            await controller.Enable(CancellationToken.None);
            marker.Received(1).SetActive(true);
        }


        public void Dispose()
        {
            controller.Dispose();
            marker.Received(1).Dispose();
        }


        public async Task DeactivateOnDisable()
        {
            await controller.Enable(CancellationToken.None);
            await controller.Disable(CancellationToken.None);

            marker.Received().SetActive(false);
        }
    }
}
