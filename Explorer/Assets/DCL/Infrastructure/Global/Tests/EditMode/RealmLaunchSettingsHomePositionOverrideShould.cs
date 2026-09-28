using DCL.FeatureFlags;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Prefs;
using DCL.Web3.Identities;
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
        private RealmLaunchSettings launchSettings = null!;
        private IAppArgs appArgs = null!;
        private IWeb3IdentityCache identityCache = null!;

        private static IDCLPrefs? originalPrefs;
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
            identityCache = new IWeb3IdentityCache.Fake();
        }

        [TearDown]
        public void TearDown()
        {
            HomeMarkerController.Serialize(identityCache, null);
            HomeMarkerController.SerializeWorldName(identityCache, null);
        }

        [Test]
        public void NotUseHomePositionOfAnotherAccount()
        {
            // Arrange
            HomeMarkerController.Serialize(identityCache, new Vector2Int(100, 200));
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;
            var featureFlags = GetFeatureFlagsConfiguration(true, "0,0");

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, new IWeb3IdentityCache.Fake());

            // Assert
            Assert.AreEqual(new Vector2Int(0, 0), launchSettings.targetScene);
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
            HomeMarkerController.Serialize(identityCache, homePosition);
            launchSettings.targetScene = new Vector2Int(0, 0);
            appArgs.HasFlag(AppArgsFlags.POSITION).Returns(true);
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

            // Assert
            Assert.AreEqual(new Vector2Int(0, 0), launchSettings.targetScene);
        }

        [Test]
        public void NotUseHomePositionWhenEditorOverrideActive()
        {
            // Arrange
            var homePosition = new Vector2Int(100, 200);
            HomeMarkerController.Serialize(identityCache, homePosition);
            launchSettings.HasEditorPositionOverride().Returns(true);
            launchSettings.targetScene = new Vector2Int(50, 50);
            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

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
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

            // Assert
            Assert.AreEqual(new Vector2Int(75, 80), launchSettings.targetScene);
        }

        [Test]
        public void UseHomePositionWhenFeatureFlagIsDefaultButHomeExists()
        {
            // Arrange
            var homePosition = new Vector2Int(100, 200);
            HomeMarkerController.Serialize(identityCache, homePosition);
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;

            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            // Act
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

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
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

            // Assert
            Assert.AreEqual(initialPosition, launchSettings.targetScene);
        }

        [Test]
        public void UseWorldHomeWhenFeatureFlagIsDefaultButWorldHomeExists()
        {
            HomeMarkerController.SerializeWorldName(identityCache, "testworld.dcl.eth");
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;

            var featureFlags = GetFeatureFlagsConfiguration(true, "0,0");

            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);
            Assert.AreEqual(InitialRealm.World, launchSettings.initialRealm);
        }

        [Test]
        public void UseDeepLinkRealmOverSavedWorldHome()
        {
            HomeMarkerController.SerializeWorldName(identityCache, "myhome.dcl.eth");
            launchSettings.EditorSceneStartPosition = false;
            appArgs.HasFlag(AppArgsFlags.REALM).Returns(true);

            appArgs.TryGetValue(AppArgsFlags.REALM, out Arg.Any<string?>())
                   .Returns(call =>
                    {
                        call[1] = "cozyfarm.dcl.eth";
                        return true;
                    });

            var featureFlags = GetFeatureFlagsConfiguration(true, "0,0");

            launchSettings.ApplyConfig(appArgs);
            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

            Assert.AreEqual(InitialRealm.World, launchSettings.initialRealm);
            Assert.AreEqual("cozyfarm.dcl.eth", launchSettings.TargetWorld);
        }

        [Test]
        public void NotUseHomePositionWhenDeepLinkRealmProvided()
        {
            HomeMarkerController.Serialize(identityCache, new Vector2Int(100, 200));
            launchSettings.targetScene = new Vector2Int(5, 5);
            launchSettings.EditorSceneStartPosition = false;
            appArgs.HasFlag(AppArgsFlags.REALM).Returns(true);

            var featureFlags = GetFeatureFlagsConfiguration(false, "0,0");

            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);

            Assert.AreEqual(new Vector2Int(5, 5), launchSettings.targetScene);
        }

        [Test]
        public void PreferWorldHomeOverCoordinateHome()
        {
            HomeMarkerController.Serialize(identityCache, new Vector2Int(100, 200));
            HomeMarkerController.SerializeWorldName(identityCache, "testworld.dcl.eth");
            launchSettings.targetScene = new Vector2Int(0, 0);
            launchSettings.EditorSceneStartPosition = false;

            string featureFlagPosition = "0,0";
            var featureFlags = GetFeatureFlagsConfiguration(true, featureFlagPosition);

            launchSettings.CheckStartParcelOverride(appArgs, featureFlags, identityCache);
            Assert.AreEqual(InitialRealm.World, launchSettings.initialRealm);
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
