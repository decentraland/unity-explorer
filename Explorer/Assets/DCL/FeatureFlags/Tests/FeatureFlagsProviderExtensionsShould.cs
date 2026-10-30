using DCL.Prefs;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Reflection;

namespace DCL.FeatureFlags.Tests
{
    [TestFixture]
    public class FeatureFlagsProviderExtensionsShould
    {
        private static readonly FieldInfo PREFS_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private IAppArgs appArgs = null!;
        private object? originalPrefs;

        [SetUp]
        public void SetUp()
        {
            appArgs = Substitute.For<IAppArgs>();
            originalPrefs = PREFS_FIELD.GetValue(null);
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
        }

        [TearDown]
        public void TearDown()
        {
            PREFS_FIELD.SetValue(null, originalPrefs);
        }

        [Test]
        public void PreferOverrideArgument()
        {
            GivenArg(AppArgsFlags.FeatureFlags.USER_ID, "0xoverride");
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            Assert.AreEqual("0xoverride", FeatureFlagsProviderExtensions.ResolveUserId(appArgs));
        }

        [Test]
        public void UseCampaignAnonUserId()
        {
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            Assert.AreEqual("campaign-anon-id", FeatureFlagsProviderExtensions.ResolveUserId(appArgs));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void IgnoreBlankOverride(string overridenUserId)
        {
            GivenArg(AppArgsFlags.FeatureFlags.USER_ID, overridenUserId);
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            Assert.AreEqual("campaign-anon-id", FeatureFlagsProviderExtensions.ResolveUserId(appArgs));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void IgnoreBlankCampaignId(string campaignAnonUserId)
        {
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, campaignAnonUserId);

            Assert.IsTrue(Guid.TryParse(FeatureFlagsProviderExtensions.ResolveUserId(appArgs), out _));
        }

        [Test]
        public void NotPersistTheCampaignAnonUserId()
        {
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            FeatureFlagsProviderExtensions.ResolveUserId(appArgs);

            Assert.IsEmpty(DCLPlayerPrefs.GetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID));
        }

        [Test]
        public void PreferCampaignAnonUserIdOverPersistedId()
        {
            DCLPlayerPrefs.SetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID, "already-persisted");
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            Assert.AreEqual("campaign-anon-id", FeatureFlagsProviderExtensions.ResolveUserId(appArgs));
            Assert.AreEqual("already-persisted", DCLPlayerPrefs.GetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID));
        }

        [Test]
        public void GenerateAndPersistIdWhenNoArgumentIsPresent()
        {
            string resolved = FeatureFlagsProviderExtensions.ResolveUserId(appArgs);

            Assert.IsTrue(Guid.TryParse(resolved, out _));
            Assert.AreEqual(resolved, DCLPlayerPrefs.GetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID));
        }

        [Test]
        public void ReusePersistedIdAcrossCalls()
        {
            string first = FeatureFlagsProviderExtensions.ResolveUserId(appArgs);
            string second = FeatureFlagsProviderExtensions.ResolveUserId(appArgs);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void NotPersistTheOverride()
        {
            GivenArg(AppArgsFlags.FeatureFlags.USER_ID, "0xoverride");

            FeatureFlagsProviderExtensions.ResolveUserId(appArgs);

            Assert.IsEmpty(DCLPlayerPrefs.GetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID));
        }

        private void GivenArg(string flag, string value)
        {
            appArgs.TryGetValue(flag, out Arg.Any<string?>())
                   .Returns(call =>
                    {
                        call[1] = value;
                        return true;
                    });
        }
    }
}
