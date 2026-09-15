using DCL.FeatureFlags;
using Global.AppArgs;
using NUnit.Framework;

namespace DCL.ExplorePanel.Lobby.Tests
{
    public class LobbyEntryShould
    {
        [Test]
        public void RespectTheFlagAndExplicitLaunchIntent()
        {
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsResultDto flags = FeatureFlagsResultDto.Empty;
            flags.flags[FeatureFlagsStrings.LIVING_LOBBY] = true;
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(flags));
            try
            {
                var enabled = new ApplicationParametersParser(false);
                var disabled = new ApplicationParametersParser(false, "--living-lobby", "false", "--open-lobby", "true");
                Assert.That(new FeaturesRegistry(enabled, false).IsEnabled(FeatureId.LivingLobby), Is.True);
                Assert.That(new FeaturesRegistry(disabled, false).IsEnabled(FeatureId.LivingLobby), Is.False);
                Assert.That(LobbyStartup.ShouldOpen(enabled, true), Is.True);
                Assert.That(LobbyStartup.ShouldOpen(disabled, false), Is.False);
                Assert.That(LobbyStartup.ShouldOpen(new ApplicationParametersParser(false, "--position", "40,54"), true), Is.False);
                flags.flags.Clear();
                Assert.That(new FeaturesRegistry(enabled, false).IsEnabled(FeatureId.LivingLobby), Is.False);
                var forced = new ApplicationParametersParser(false, "--living-lobby", "true");
                Assert.That(new FeaturesRegistry(forced, false).IsEnabled(FeatureId.LivingLobby), Is.True);
            }
            finally
            {
                FeatureFlagsConfiguration.Reset();
            }
        }
    }
}
