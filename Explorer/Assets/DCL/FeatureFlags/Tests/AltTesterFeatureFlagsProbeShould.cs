#if ALTTESTER
using Global.AppArgs;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;

namespace DCL.FeatureFlags.Tests
{
    public class AltTesterFeatureFlagsProbeShould
    {
        private const string FLAGS_JSON = @"{""flags"":{""alfa-marketplace-credits"":true,""alfa-friends"":true,""disabled-ff"":false},""variants"":{""alfa-marketplace-credits"":{""name"":""wallets"",""payload"":{""type"":""string"",""value"":""0x1,0x2""},""enabled"":true}}}";

        [SetUp]
        public void SetUp()
        {
            // Other suites in this assembly initialize the singletons without resetting them.
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [TestCase("alfa-marketplace-credits", true)]
        [TestCase("alfa-friends", true)]
        [TestCase("disabled-ff", false)]
        [TestCase("absent-ff", false)]
        public void ReadRawFlagState(string flagId, bool expected)
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);

            // Act
            bool enabled = AltTesterFeatureFlagsProbe.IsFlagEnabled(flagId);

            // Assert
            Assert.AreEqual(expected, enabled);
        }

        [TestCase("MarketplaceCredits")]
        [TestCase("marketplacecredits")]
        public void ResolveFeatureIdCaseInsensitively(string featureId)
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);
            InitializeRegistry();

            // Act
            bool enabled = AltTesterFeatureFlagsProbe.IsFeatureEnabled(featureId);

            // Assert
            Assert.IsTrue(enabled);
        }

        [Test]
        public void ReadFeatureStateFromRegistryNotFromFlag()
        {
            // Arrange — the flag drives MarketplaceCredits, so an empty document turns it off.
            InitializeFlags(@"{""flags"":{},""variants"":{}}");
            InitializeRegistry();

            // Act
            bool enabled = AltTesterFeatureFlagsProbe.IsFeatureEnabled("MarketplaceCredits");

            // Assert
            Assert.IsFalse(enabled);
        }

        [TestCase("NoSuchFeature")]
        [TestCase("9999")]
        [TestCase("")]
        public void ThrowOnUnknownFeatureId(string featureId)
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);
            InitializeRegistry();

            // Act / Assert — a typo must fail loudly instead of reading as "off".
            Assert.Throws<ArgumentException>(() => AltTesterFeatureFlagsProbe.IsFeatureEnabled(featureId));
        }

        [Test]
        public void ReadVariantPayload()
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);

            // Act
            var variant = JsonConvert.DeserializeObject<VariantDto>(
                AltTesterFeatureFlagsProbe.GetFlagVariantJson("alfa-marketplace-credits"));

            // Assert
            Assert.IsTrue(variant.present);
            Assert.AreEqual("wallets", variant.name);
            Assert.IsTrue(variant.enabled);
            Assert.AreEqual("string", variant.payloadType);
            Assert.AreEqual("0x1,0x2", variant.payloadValue);
        }

        [Test]
        public void ReportAbsentVariant()
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);

            // Act
            var variant = JsonConvert.DeserializeObject<VariantDto>(
                AltTesterFeatureFlagsProbe.GetFlagVariantJson("alfa-friends"));

            // Assert
            Assert.IsFalse(variant.present);
        }

        [Test]
        public void ListEnabledFlagsAndFeaturesInStatus()
        {
            // Arrange
            InitializeFlags(FLAGS_JSON);
            InitializeRegistry();

            // Act
            StatusDto status = JsonConvert.DeserializeObject<StatusDto>(AltTesterFeatureFlagsProbe.GetStatusJson());

            // Assert
            Assert.IsTrue(status.flagsLoaded);
            Assert.IsTrue(status.registryLoaded);
            Assert.Contains("alfa-marketplace-credits", status.enabledFlags);
            Assert.Contains("alfa-friends", status.enabledFlags);
            Assert.IsFalse(Array.Exists(status.enabledFlags, flag => flag == "disabled-ff"));
            Assert.Contains("MarketplaceCredits", status.enabledFeatures);
        }

        [Test]
        public void ReportNotLoadedInStatusInsteadOfThrowing()
        {
            // Arrange — nothing initialized, as when a test probes before login completes.

            // Act
            StatusDto status = JsonConvert.DeserializeObject<StatusDto>(AltTesterFeatureFlagsProbe.GetStatusJson());

            // Assert
            Assert.IsFalse(status.flagsLoaded);
            Assert.IsFalse(status.registryLoaded);
        }

        private static void InitializeFlags(string json) =>
            FeatureFlagsConfiguration.Initialize(
                new FeatureFlagsConfiguration(JsonConvert.DeserializeObject<FeatureFlagsResultDto>(json)));

        private static void InitializeRegistry() =>
            FeaturesRegistry.Initialize(new FeaturesRegistry(Substitute.For<IAppArgs>(), false));

        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private struct VariantDto
        {
            public bool present;
            public string name;
            public bool enabled;
            public string payloadType;
            public string payloadValue;
        }

        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private struct StatusDto
        {
            public bool flagsLoaded;
            public bool registryLoaded;
            public string[] enabledFlags;
            public string[] enabledFeatures;
        }
    }
}
#endif
