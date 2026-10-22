using DCL.MapRenderer.MapLayers.HomeMarker;
using NUnit.Framework;
using UnityEngine;

namespace DCL.MapRenderer.Tests.HomeMarker
{
    public class HomeMarkerShould
    {
        private HomeMarkerObject markerObject = null!;
        private MapLayers.HomeMarker.HomeMarker marker = null!;

        [SetUp]
        public void SetUp()
        {
            markerObject = new GameObject("home marker").AddComponent<HomeMarkerObject>();
            marker = new MapLayers.HomeMarker.HomeMarker(markerObject);
        }

        [TearDown]
        public void TearDown()
        {
            marker.Dispose();
        }

        [TestCase(5f, 0.5f)] // zoomed in 2x past the base zoom: the on-screen size stays the same
        [TestCase(20f, 2f)] // zoomed out 2x: the marker keeps its size on screen too
        public void ScaleWithTheZoomRelativeToTheBaseZoom(float zoom, float expectedScale)
        {
            // Act
            marker.SetZoom(baseScale: 1f, baseZoom: 10f, zoom: zoom);

            // Assert
            Assert.AreEqual(expectedScale, markerObject.transform.localScale.x, 1e-5f);
        }
    }
}
