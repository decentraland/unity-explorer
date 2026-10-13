using DCL.Prefs;
using Global.AppArgs;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Reflection;

namespace DCL.FeatureFlags.Tests
{
    /// <summary>
    ///     The lobby analytics tell a session that rolled the lobby from the flag from one that already had the setting stored.
    /// </summary>
    [TestFixture]
    public class FeaturesRegistryLobbyShould
    {
        // The lobby setting reads player prefs, which the editor never initializes; see ChatBubblesVisibilityControllerShould
        private static readonly FieldInfo DCL_PREFS_BACKING_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private IDCLPrefs originalPrefs = null!;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = (IDCLPrefs)DCL_PREFS_BACKING_FIELD.GetValue(null)!;
            DCL_PREFS_BACKING_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
            FeatureFlagsConfiguration.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FeatureFlagsConfiguration.Reset();
            DCL_PREFS_BACKING_FIELD.SetValue(null, originalPrefs);
        }

        [Test]
        public void ReportGrandfatheredWhenTheSettingWasStoredBeforeTheSession()
        {
            // Arrange
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_LOBBY_ENABLED, false);
            FeaturesRegistry registry = CreateRegistry(lobbyFlagEnabled: true);

            // Act
            bool enabled = registry.LobbyEnabledSetting;

            // Assert
            Assert.IsFalse(enabled);
            Assert.IsTrue(registry.LobbyGrandfathered);
        }

        [Test]
        public void NotReportGrandfatheredWhenThisSessionsFlagRollPersistsTheSetting()
        {
            // Arrange
            FeaturesRegistry registry = CreateRegistry(lobbyFlagEnabled: true);

            // Act
            bool enabled = registry.LobbyEnabledSetting;

            // Assert
            Assert.IsTrue(enabled);
            Assert.IsTrue(DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_LOBBY_ENABLED));
            Assert.IsFalse(registry.LobbyGrandfathered);
        }

        [Test]
        public void NotReportGrandfatheredWhenNothingIsStoredAndTheFlagIsOff()
        {
            // Arrange
            FeaturesRegistry registry = CreateRegistry(lobbyFlagEnabled: false);

            // Act
            bool enabled = registry.LobbyEnabledSetting;

            // Assert
            Assert.IsFalse(enabled);
            Assert.IsFalse(DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_LOBBY_ENABLED));
            Assert.IsFalse(registry.LobbyGrandfathered);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResolveGrandfatheredOnDemandBeforeTheSettingIsRead(bool stored)
        {
            // Arrange
            if (stored)
                DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_LOBBY_ENABLED, true);

            FeaturesRegistry registry = CreateRegistry(lobbyFlagEnabled: true);

            // Act
            bool grandfatheredBeforeTheSetting = registry.LobbyGrandfathered;
            bool enabled = registry.LobbyEnabledSetting;

            // Assert
            Assert.IsTrue(enabled);
            Assert.AreEqual(stored, grandfatheredBeforeTheSetting);
            Assert.AreEqual(stored, registry.LobbyGrandfathered);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExposeTheRemoteFlagRegardlessOfTheStoredSetting(bool lobbyFlagEnabled)
        {
            // Arrange
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_LOBBY_ENABLED, !lobbyFlagEnabled);

            // Act
            FeaturesRegistry registry = CreateRegistry(lobbyFlagEnabled);

            // Assert
            Assert.AreEqual(lobbyFlagEnabled, registry.LobbyFlagEnabled);
            Assert.AreEqual(!lobbyFlagEnabled, registry.LobbyEnabledSetting);
        }

        private static FeaturesRegistry CreateRegistry(bool lobbyFlagEnabled)
        {
            string json = $"{{\"flags\":{{\"{FeatureFlagsStrings.LOBBY}\":{(lobbyFlagEnabled ? "true" : "false")}}},\"variants\":{{}}}}";
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(JsonConvert.DeserializeObject<FeatureFlagsResultDto>(json)!));

            return new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false);
        }
    }
}
