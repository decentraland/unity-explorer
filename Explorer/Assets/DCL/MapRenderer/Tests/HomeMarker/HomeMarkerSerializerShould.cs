using System.Reflection;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Prefs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.MapRenderer.Tests.HomeMarker
{
	[TestFixture]
	public class HomeMarkerSerializerShould
	{
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
			// Clear prefs keys (safe because player prefs are replaced for test duration)
			if (DCLPlayerPrefs.HasKey(DCLPrefKeys.MAP_HOME_MARKER_DATA))
				DCLPlayerPrefs.DeleteKey(DCLPrefKeys.MAP_HOME_MARKER_DATA);
		}

		[TearDown]
		public void TearDown()
		{
			// Clean up test data
			if (DCLPlayerPrefs.HasKey(DCLPrefKeys.MAP_HOME_MARKER_DATA))
				DCLPlayerPrefs.DeleteKey(DCLPrefKeys.MAP_HOME_MARKER_DATA);
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
		public void SerializeAndDeserializeCorrectly()
		{
			// Arrange
			var position = new Vector2Int(42, 84);
			var homeData = new HomeMarkerData(position);

			// Act
			HomeMarkerSerializer.Serialize(homeData);
			var deserialized = HomeMarkerSerializer.Deserialize();

			// Assert
			Assert.IsTrue(deserialized.HasValue);
			Assert.AreEqual(position, deserialized!.Value.Position);
		}

		[Test]
		public void ReturnNullWhenNoDataSerialized()
		{
			// Act
			var deserialized = HomeMarkerSerializer.Deserialize();

			// Assert
			Assert.IsFalse(deserialized.HasValue);
		}

		[Test]
		public void RemoveCorruptedData()
		{
			// Arrange
			DCLPlayerPrefs.SetString(DCLPrefKeys.MAP_HOME_MARKER_DATA, "corrupted_data");

			// Act
			// Silence LogError about expected deserialization of data.
			LogAssert.ignoreFailingMessages = true; 
			var deserialized = HomeMarkerSerializer.Deserialize();
			LogAssert.ignoreFailingMessages = false;

			// Assert
			Assert.IsFalse(deserialized.HasValue);
			Assert.IsFalse(HomeMarkerSerializer.HasSerializedPosition());
		}

		[Test]
		public void DeleteDataWhenSerializingNull()
		{
			// Arrange
			var homeData = new HomeMarkerData(new Vector2Int(10, 20));
			HomeMarkerSerializer.Serialize(homeData);
			Assert.IsTrue(HomeMarkerSerializer.HasSerializedPosition());

			// Act
			HomeMarkerSerializer.Serialize(null);

			// Assert
			Assert.IsFalse(HomeMarkerSerializer.HasSerializedPosition());
		}
	}
}

