using System.Reflection;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.Prefs;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.Tests.HomeMarker
{
	[TestFixture]
	public class HomeMarkerControllerShould
	{
		private HomeMarkerController controller;
		private IHomeMarker marker;
		private ICoordsUtils coordsUtils;
		private IMapCullingController cullingController;
		private INavmapBus navmapBus;
		private IPlacesAPIService placesAPIService;
		private HomePlaceEventBus homePlaceEventBus;
		private IEventBus eventBus;
		private Transform parent;
		
		private static IDCLPrefs originalPrefs;
		private static bool prefsInitialized;
		
		[OneTimeSetUp]
		public void OneTimeSetup()
		{
			// Initialize DCLPlayerPrefs with InMemoryDCLPlayerPrefs implementation
			InitializeTestPrefs();
		}

		[OneTimeTearDown]
		public void OneTimeTearDown()
		{
			// Restore original implementation if it existed
			RestoreOriginalPrefs();
		}
		
		[SetUp]
		public void Setup()
		{
			parent = new GameObject("Parent").transform;
			marker = Substitute.For<IHomeMarker>();
			coordsUtils = Substitute.For<ICoordsUtils>();
			cullingController = Substitute.For<IMapCullingController>();
			navmapBus = Substitute.For<INavmapBus>();
			placesAPIService = Substitute.For<IPlacesAPIService>();
			homePlaceEventBus = new HomePlaceEventBus();
			eventBus = Substitute.For<IEventBus>();			

			coordsUtils.CoordsToPositionWithOffset(Arg.Any<Vector2>())
				.Returns(callInfo => 
				{
					var coords = callInfo.Arg<Vector2>();
					return new Vector3(coords.x, coords.y, 0);
				});

			controller = new HomeMarkerController(
				(p) => marker,
				parent,
				coordsUtils,
				cullingController,
				navmapBus,
				placesAPIService,
				eventBus
			);
			homePlaceEventBus.Controller = controller;

			// Clear prefs keys (safe because player prefs are replaced for test duration)
			if (DCLPlayerPrefs.HasVectorKey(DCLPrefKeys.MAP_HOME_MARKER_DATA))
				DCLPlayerPrefs.DeleteVector2Key(DCLPrefKeys.MAP_HOME_MARKER_DATA);
		}
		
		[TearDown]
        public void TearDown()
        {
			controller?.Dispose();
			if (parent != null && parent.gameObject != null)
				Object.DestroyImmediate(parent.gameObject);
	        
			// Clean up test data
			if (DCLPlayerPrefs.HasVectorKey(DCLPrefKeys.MAP_HOME_MARKER_DATA))
				DCLPlayerPrefs.DeleteVector2Key(DCLPrefKeys.MAP_HOME_MARKER_DATA);
        }
		
		private static void InitializeTestPrefs()
		{
			var dclPrefsField = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);
            
			if (dclPrefsField != null)
			{
				var currentPrefs = dclPrefsField.GetValue(null) as IDCLPrefs;
                
				if (currentPrefs == null)
				{
					var testPrefs = new InMemoryDCLPlayerPrefs();
					dclPrefsField.SetValue(null, testPrefs);
					prefsInitialized = true;
				}
				else
				{
					// Store the original if it exists
					originalPrefs = currentPrefs;
					// Replace prefs for tests
					var testPrefs = new InMemoryDCLPlayerPrefs();
					dclPrefsField.SetValue(null, testPrefs);
				}
			}
		}
		
		private static void RestoreOriginalPrefs()
		{
			if (!prefsInitialized && originalPrefs != null)
			{
				var dclPrefsField = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);
				dclPrefsField?.SetValue(null, originalPrefs);
			}
		}

        [Test]
        public void InitializeWithSerializedHomePosition()
        {
            // Arrange
            Vector2Int? homePosition = new Vector2Int(10, 20);
	        controller.Initialize();

            // Act
	        controller.SetMarker(homePosition);

            // Assert
            marker.Received(1).SetActive(true);
            marker.Received(1).SetPosition(new Vector3(10, 20, 0));
            Assert.IsTrue(controller.HomeIsSet);
        }

        [Test]
        public void InitializeWithoutSerializedHomePosition()
        {
            // Act
            controller.Initialize();

            // Assert
            marker.Received(1).SetActive(false);
            Assert.IsFalse(controller.HomeIsSet);
        }

        [Test]
        public void SetHomeWhenRequested()
        {
            // Arrange
            controller.Initialize();
            var newHomePosition = new Vector2Int(15, 25);

            // Act
            homePlaceEventBus.SetAsHome(newHomePosition);

            // Assert
            marker.Received(1).SetActive(true);
            marker.Received(1).SetPosition(new Vector3(15, 25, 0));
            Assert.IsTrue(controller.HomeIsSet);
            Assert.IsTrue(HomeMarkerController.HasSerializedPosition());
        }

        [Test]
        public void UnsetHomeWhenRequested()
        {
            // Arrange
            controller.Initialize();
            homePlaceEventBus.SetAsHome(new Vector2Int(5, 10));

            // Act
            homePlaceEventBus.UnsetHome();

            // Assert
            marker.Received(2).SetActive(false); // Once on unset, once initially
            Assert.IsFalse(controller.HomeIsSet);
            Assert.IsFalse(HomeMarkerController.HasSerializedPosition());
        }

        [Test]
        public void CorrectlyIdentifyHomeCoordinates()
        {
            // Arrange
            controller.Initialize();
            var homePosition = new Vector2Int(30, 40);
            homePlaceEventBus.SetAsHome(homePosition);

            // Act & Assert
            Assert.IsTrue(homePlaceEventBus.CurrentHomeCoordinates == homePosition);
            Assert.IsFalse(homePlaceEventBus.CurrentHomeCoordinates == Vector2Int.zero);
        }

        [Test]
        public void GetHomeCoordinatesWhenSet()
        {
            // Arrange
            controller.Initialize();
            var homePosition = new Vector2Int(50, 60);

            // Act
	        homePlaceEventBus.SetAsHome(homePosition);

            // Assert
	        Assert.IsTrue(homePlaceEventBus.CurrentHomeCoordinates != null);
            Assert.AreEqual(homePosition, homePlaceEventBus.CurrentHomeCoordinates);
        }

        [Test]
        public void NotGetHomeCoordinatesWhenNotSet()
        {
            // Arrange
            controller.Initialize();

            // Assert
            Assert.IsTrue(homePlaceEventBus.CurrentHomeCoordinates == null);
        }
	}
}