using DCL.FeatureFlags;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Prefs;
using Global.AppArgs;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Global.Tests.EditMode
{
	[TestFixture]
	public class RealmLaunchSettingsHomePositionOverrideShould
	{
		private RealmLaunchSettings launchSettings;
		private IAppArgs appArgs;
        
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
            launchSettings = new RealmLaunchSettings();
            appArgs = Substitute.For<IAppArgs>();
        }

        [TearDown]
        public void TearDown()
        {
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
        public void NotUseHomePositionWhenAppArgPositionExists()
        {
            // Arrange
            var homePosition = new Vector2Int(100, 200);
            HomeMarkerController.Serialize(homePosition);
            launchSettings.targetScene = new Vector2Int(0, 0);
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(true);
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags);

            // Assert
            Assert.AreEqual(new Vector2Int(0, 0), launchSettings.targetScene);
        }

        [Test]
        public void NotUseHomePositionWhenEditorOverrideActive()
        {
            // Arrange
            var homePosition = new Vector2Int(100, 200);
            HomeMarkerController.Serialize(homePosition);
            launchSettings.HasEditorPositionOverride().Returns(true);
            launchSettings.targetScene = new Vector2Int(50, 50);
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);
            
            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags);

            // Assert
            Assert.AreEqual(new Vector2Int(50, 50), launchSettings.targetScene);
        }

        [Test]
        public void UseFeatureFlagPositionWhenNoHomeAndDefaultPosition()
        {
            // Arrange
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;

            string featureFlagPosition = "75,80";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags);

            // Assert
            Assert.AreEqual(new Vector2Int(75, 80), launchSettings.targetScene);
        }

        [Test]
        public void UseHomePositionWhenFeatureFlagIsDefaultButHomeExists()
        {
            // Arrange
            var homePosition = new Vector2Int(100, 200);
            HomeMarkerController.Serialize(homePosition);
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;
            
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags);

            // Assert
            Assert.AreEqual(homePosition, launchSettings.targetScene);
        }

        [Test]
        public void NotChangePositionWhenFeatureFlagDisabled()
        {
            // Arrange
            var initialPosition = new Vector2Int(10, 10);
            launchSettings.targetScene = initialPosition;
            
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(false, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags);

            // Assert
            Assert.AreEqual(initialPosition, launchSettings.targetScene);
        }

        private FeatureFlagsConfiguration GetFeatureFlagsConfiguration(bool returns, string position)
        {
            var resultDto = new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>
                {
                    { FeatureFlagsStrings.GENESIS_STARTING_PARCEL, returns }
                },
                variants = new Dictionary<string, FeatureFlagVariantDto>
                {
                    {
                        FeatureFlagsStrings.GENESIS_STARTING_PARCEL,
                        new FeatureFlagVariantDto
                        {
                            name = FeatureFlagsStrings.STRING_VARIANT,
                            enabled = returns,
                            payload = new FeatureFlagPayload
                            {
                                type = "string",
                                value = position
                            }
                        }
                    }
                }
            };

            return new FeatureFlagsConfiguration(resultDto);
        }
	}
}

