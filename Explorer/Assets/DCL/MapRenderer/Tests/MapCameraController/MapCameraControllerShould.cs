﻿using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.Navmap;
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

        [SetUp]
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

        [Test]
        public void BeConstructed()
        {
            Assert.AreEqual(mapCamera.MapCameraObject, mapCameraObject);
            Assert.AreEqual(mapCamera.CoordUtils, coordsUtils);
            Assert.IsTrue(mapCameraObject.mapCamera.orthographic);
            Assert.IsNull(mapCamera.RenderTexture);
        }

        [Test]
        public void BeInitialized()
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(30, 30), new Vector2Int(10, 20), MapLayer.SatelliteAtlas);

            Assert.NotNull(mapCamera.RenderTexture);
            Assert.AreEqual(30, mapCamera.RenderTexture.width);
            Assert.AreEqual(30, mapCamera.RenderTexture.height);
            Assert.AreEqual(MapLayer.SatelliteAtlas, mapCamera.EnabledLayers);
            Assert.AreEqual(new Vector2Int(100, 200), mapCamera.ZoomValues);
        }

        [Test]
        public void ThrowIfAccessingRenderTextureWhenNotInitialized()
        {
            Assert.Throws<Exception>(() => mapCamera.GetRenderTexture());
        }

        [Test]
        public void ReturnRenderTexture()
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), Vector2Int.one, MapLayer.SatelliteAtlas);

            var renderTexture = mapCamera.GetRenderTexture();

            Assert.AreEqual(mapCamera.RenderTexture, renderTexture);
        }

        [TestCase(-5, 100, 200, 200)]
        [TestCase(0, 100, 200, 200)]
        [TestCase(0.75f, 100, 200, 125)]
        [TestCase(0.5f, 100, 200, 150)]
        [TestCase(0.25f, 100, 200, 175)]
        [TestCase(1f, 100, 200, 100)]
        [TestCase(2f, 100, 200, 100)]
        public void SetZoom(float zoom, int minZoom, int maxZoom, float expected)
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), new Vector2Int(100, 200), MapLayer.SatelliteAtlas);

            mapCamera.SetZoom(zoom, 0);

            Assert.AreEqual(expected, mapCameraObject.mapCamera.orthographicSize / coordsUtils.ParcelSize);
            culling.Received().SetCameraDirty(mapCamera);
        }

        [Test]
        public void SetPosition()
        {
            coordsUtils.VisibleWorldBounds.Returns(Rect.MinMaxRect(-1000, -1000, 1000, 1000));
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), new Vector2Int(10, 20), MapLayer.SatelliteAtlas);
            mapCamera.SetZoom(0, 0);

            coordsUtils.CoordsToPositionUnclamped(Arg.Any<Vector2>()).Returns((x) => (Vector3)x.ArgAt<Vector2>(0) * 10); //Multiply input by 10

            mapCamera.SetPosition(Vector2.one);

            Assert.AreEqual(new Vector3(10, 10, mapCamera.CAMERA_HEIGHT_EXPOSED), mapCameraObject.transform.localPosition);
            culling.Received().SetCameraDirty(mapCamera);
        }

        [Test]
        [TestCaseSource(nameof(LocalPositionTestCases))]
        public void SetLocalPosition(Vector2 desired, Vector2 expected, Vector2Int zoomValues, float zoom)
        {
            ((IMapCameraControllerInternal)mapCamera).Initialize(new Vector2Int(20, 20), zoomValues, MapLayer.SatelliteAtlas);
            mapCamera.SetZoom(zoom, 0);
            mapCamera.SetLocalPosition(desired);

            Assert.AreEqual(new Vector3(expected.x, expected.y, mapCamera.CAMERA_HEIGHT_EXPOSED), mapCameraObject.transform.localPosition);
            culling.Received().SetCameraDirty(mapCamera);
        }

        public static object[] LocalPositionTestCases =
        {
            new object[] { new Vector2(10, 10), new Vector2(10, 10), new Vector2Int(10, 20), 0f},
            new object[] { new Vector2(-500, -300), new Vector2(-500, -300), new Vector2Int(10, 20), 0.5f},
            new object[] { new Vector2(-1000, 1200), new Vector2(-930, 930), new Vector2Int(10, 20), 1f},
            new object[] { new Vector2(-1000, 1200), new Vector2(-860, 860), new Vector2Int(10, 20), 0f},
            new object[] { new Vector2(4000, -8000), new Vector2(720, -720), new Vector2Int(30, 50), 0.5f},
        };

        [TestCase(true)]
        [TestCase(false)]
        public void SetActive(bool value)
        {
            mapCamera.SetActive(value);

            Assert.AreEqual(value, mapCameraObject.isActiveAndEnabled);
        }

        [TearDown]
        public void TearDown()
        {
            mapCamera.Dispose();
        }
    }
}
