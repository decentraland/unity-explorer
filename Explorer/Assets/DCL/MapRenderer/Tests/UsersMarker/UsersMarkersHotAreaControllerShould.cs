using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Users;
using DCL.MapRenderer.MapLayers.UsersMarker;
using DCL.Multiplayer.Connectivity;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.Tests.UsersMarker
{
    public class UsersMarkersHotAreaControllerShould
    {
        private const int PARCEL_SIZE = 20;
        private const float BASE_ZOOM = 10f;

        private GameObject parent = null!;
        private IHotUserMarker marker = null!;
        private UsersMarkersHotAreaController controller = null!;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("hot users markers");
            marker = Substitute.For<IHotUserMarker>();
            marker.SetScale(Arg.Any<float>()).Returns(marker);

            IObjectPool<IHotUserMarker> wrapsPool = Substitute.For<IObjectPool<IHotUserMarker>>();
            wrapsPool.Get().Returns(marker);

            ICoordsUtils coordsUtils = Substitute.For<ICoordsUtils>();
            coordsUtils.ParcelSize.Returns(PARCEL_SIZE);

            IOnlineUsersProvider onlineUsersProvider = Substitute.For<IOnlineUsersProvider>();

            onlineUsersProvider.GetAsync(Arg.Any<CancellationToken>())
                               .Returns(UniTask.FromResult<IReadOnlyCollection<OnlineUserData>>(new[] { new OnlineUserData { avatarId = "0xremote" } }));

            controller = new UsersMarkersHotAreaController(Substitute.For<IObjectPool<HotUserMarkerObject>>(), wrapsPool, parent.transform, coordsUtils,
                Substitute.For<IMapCullingController>(), Substitute.For<IRealmNavigator>(), onlineUsersProvider, Substitute.For<IWeb3IdentityCache>());
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(parent);
        }

        [TestCase(5f, PARCEL_SIZE * 0.5f)] // zoomed in 2x past the base zoom: the on-screen size stays the same
        [TestCase(20f, (float)PARCEL_SIZE)] // zoomed out: the marker keeps its size on the map
        public async Task ScaleTheRemoteUsersMarkersWithTheZoom(float zoom, float expectedScale)
        {
            // Arrange: the remote user's marker is rented when the layer is enabled
            await controller.EnableAsync(CancellationToken.None);
            marker.ClearReceivedCalls();

            // Act
            controller.ApplyCameraZoom(BASE_ZOOM, zoom, zoomLevel: 0);

            // Assert
            marker.Received(1).SetScale(expectedScale);
        }
    }
}
