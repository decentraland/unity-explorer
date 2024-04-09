using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.MapRenderer.Tests.MapCameraController
{
    public class MapCameraControllerShould
    {
        private DCL.MapRenderer.MapCameraController.MapCameraController mapCamera;
        private MapCameraObject mapCameraObject;
        private ICoordsUtils coordsUtils;
        private IMapCullingController culling;


        public void SetUp()
        {
            GameObject go = new GameObject();
            mapCameraObject = go.AddComponent<MapCameraObject>();
            mapCameraObject.mapCamera = go.AddComponent<Camera>();

            coordsUtils = Substitute.For<ICoordsUtils>();
            coordsUtils.ParcelSize.Returns(10);
            coordsUtils.VisibleWorldBounds.Returns(Rect.MinMaxRect(-1000, -1000, 1000, 1000));

            culling = Substitute.For<IMapCullingController>();

            mapCamera = new DCL.MapRenderer.MapCameraController.MapCameraController(
                Substitute.For<IMapInteractivityControllerInternal>(), mapCameraObject, coordsUtils, culling);
        }


        public void BeConstructed()
        {
            Assert.AreEqual(mapCamera.MapCameraObject, mapCameraObject);
            Assert.AreEqual(mapCamera.CoordUtils, coordsUtils);
            Assert.IsTrue(mapCameraObject.mapCamera.orthographic);
            Assert.IsNull(mapCamera.RenderTexture);
        }


        public void BeInitialized()
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(30, 30), new Vector2Int(10, 20), MapLayer.ParcelsAtlas);

            Assert.NotNull(mapCamera.RenderTexture);
            Assert.AreEqual(30, mapCamera.RenderTexture.width);
            Assert.AreEqual(30, mapCamera.RenderTexture.height);
            Assert.AreEqual(MapLayer.ParcelsAtlas, mapCamera.EnabledLayers);
            Assert.AreEqual(new Vector2Int(100, 200), mapCamera.ZoomValues);
        }


        public void ThrowIfAccessingRenderTextureWhenNotInitialized()
        {
            Assert.Throws<Exception>(() => mapCamera.GetRenderTexture());
        }


        public void ReturnRenderTexture()
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), Vector2Int.one, MapLayer.ParcelsAtlas);

            var renderTexture = mapCamera.GetRenderTexture();

            Assert.AreEqual(mapCamera.RenderTexture, renderTexture);
        }








        public void SetZoom(float zoom, int minZoom, int maxZoom, float expected)
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), new Vector2Int(100, 200), MapLayer.ParcelsAtlas);

            mapCamera.SetZoom(zoom);

            Assert.AreEqual(expected, mapCameraObject.mapCamera.orthographicSize / coordsUtils.ParcelSize);
            culling.Received().SetCameraDirty(mapCamera);
        }


        public void SetPosition()
        {
            coordsUtils.VisibleWorldBounds.Returns(Rect.MinMaxRect(-1000, -1000, 1000, 1000));
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), new Vector2Int(10, 20), MapLayer.ParcelsAtlas);
            mapCamera.SetZoom(0);

            coordsUtils.CoordsToPositionUnclamped(Arg.Any<Vector2>()).Returns((x) => (Vector3)x.ArgAt<Vector2>(0) * 10); //Multiply input by 10

            mapCamera.SetPosition(Vector2.one);

            Assert.AreEqual(new Vector3(10, 10, mapCamera.CAMERA_HEIGHT_EXPOSED), mapCameraObject.transform.localPosition);
            culling.Received().SetCameraDirty(mapCamera);
        }



        public void SetLocalPosition(Vector2 desired, Vector2 expected, Vector2Int zoomValues, float zoom)
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), zoomValues, MapLayer.ParcelsAtlas);
            mapCamera.SetZoom(zoom);
            mapCamera.SetLocalPosition(desired);

            Assert.AreEqual(new Vector3(expected.x, expected.y, mapCamera.CAMERA_HEIGHT_EXPOSED), mapCameraObject.transform.localPosition);
            culling.Received().SetCameraDirty(mapCamera);
        }

        public static object[] LocalPositionTestCases =
        {
            new object[] { new Vector2(10, 10), new Vector2(10, 10), new Vector2Int(10, 20), 0f},
            new object[] { new Vector2(-500, -300), new Vector2(-500, -300), new Vector2Int(10, 20), 0.5f},
            new object[] { new Vector2(-1000, 1200), new Vector2(-900, 900), new Vector2Int(10, 20), 1f},
            new object[] { new Vector2(-1000, 1200), new Vector2(-800, 800), new Vector2Int(10, 20), 0f},
            new object[] { new Vector2(4000, -8000), new Vector2(600, -600), new Vector2Int(30, 50), 0.5f},
        };



        public void SetActive(bool value)
        {
            mapCamera.SetActive(value);

            Assert.AreEqual(value, mapCameraObject.isActiveAndEnabled);
        }


        public void TearDown()
        {
            mapCamera.Dispose();
        }
    }
}
