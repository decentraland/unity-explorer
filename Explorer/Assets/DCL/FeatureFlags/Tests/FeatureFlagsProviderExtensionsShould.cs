using DCL.Prefs;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.FeatureFlags.Tests
{
    [TestFixture]
    public class FeatureFlagsProviderExtensionsShould
    {
        private IAppArgs appArgs = null!;
        private IDCLPrefs originalPrefs = null!;

        [SetUp]
        public void SetUp()
        {
            appArgs = Substitute.For<IAppArgs>();
            originalPrefs = DCLPlayerPrefs.SwapForTests(new InMemoryDCLPlayerPrefs());
        }

        [TearDown]
        public void TearDown()
        {
            DCLPlayerPrefs.SwapForTests(originalPrefs);
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

            Assert.IsEmpty(DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID));
        }

        [Test]
        public void PreferCampaignAnonUserIdOverPersistedId()
        {
            DCLPlayerPrefs.SetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID, "already-persisted");
            GivenArg(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, "campaign-anon-id");

            Assert.AreEqual("campaign-anon-id", FeatureFlagsProviderExtensions.ResolveUserId(appArgs));
            Assert.AreEqual("already-persisted", DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID));
        }

        [Test]
        public void GenerateAndPersistIdWhenNoArgumentIsPresent()
        {
            string resolved = FeatureFlagsProviderExtensions.ResolveUserId(appArgs);

            Assert.IsTrue(Guid.TryParse(resolved, out _));
            Assert.AreEqual(resolved, DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID));
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

            Assert.IsEmpty(DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID));
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
