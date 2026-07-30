using DCL.FeatureFlags;
using DCL.McpServer.Core;
using Global.AppArgs;
using NUnit.Framework;
using System;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     Guards the two gates that decide what an agent can reach: the <c>MCP_TEST_AUTOMATION</c> build define, and
    ///     the app args behind <see cref="FeatureId.McpServer" /> / <see cref="FeatureId.McpReflection" />. Deliberately
    ///     not itself behind the define — asserting what the gate removes is only meaningful from outside it.
    /// </summary>
    public class McpServerGatingShould
    {
        /// <summary>Tools that ship in every build: the Creator Tools scene-iteration loop.</summary>
        private const int ALWAYS_SHIPPED_TOOLS = 17;

        /// <summary>The client-UI automation and reflection tools added by <c>MCP_TEST_AUTOMATION</c>.</summary>
        private const int TEST_AUTOMATION_TOOLS = 10;

        private bool ownsFlagConfiguration;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // FeaturesRegistry reads the flag configuration while resolving unrelated features; an empty one is
            // enough here because every MCP feature resolves from app args alone. The singleton is reset once per
            // assembly rather than per test, so leave it exactly as found: initialize only when nobody else has,
            // and hand it back in that state, or the next fixture's own Initialize throws.
            try { _ = FeatureFlagsConfiguration.Instance; }
            catch (Exception)
            {
                FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
                ownsFlagConfiguration = true;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (ownsFlagConfiguration)
                FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void ShipOnlyTheToolsThisBuildAllows()
        {
            var tools = 0;

            foreach (Type type in typeof(McpTool).Assembly.GetTypes())
                if (!type.IsAbstract && type.IsSubclassOf(typeof(McpTool)))
                    tools++;

#if MCP_TEST_AUTOMATION
            Assert.That(tools, Is.EqualTo(ALWAYS_SHIPPED_TOOLS + TEST_AUTOMATION_TOOLS));
#else
            Assert.That(tools, Is.EqualTo(ALWAYS_SHIPPED_TOOLS),
                "A tool leaked into release builds: every UI-automation or reflection tool must sit behind MCP_TEST_AUTOMATION.");
#endif
        }

        [Test]
        public void RunNoServerWithoutAFlag()
        {
            FeaturesRegistry features = Resolve();

            Assert.That(features.IsEnabled(FeatureId.McpServer), Is.False);
            Assert.That(features.IsEnabled(FeatureId.McpReflection), Is.False);
        }

        [TestCase(AppArgsFlags.MCP)]
        [TestCase(AppArgsFlags.MCP_PORT)]
        public void StartTheServerWithoutReflectionForTheServerFlagAlone(string flag)
        {
            FeaturesRegistry features = Resolve(flag);

            Assert.That(features.IsEnabled(FeatureId.McpServer), Is.True);
            Assert.That(features.IsEnabled(FeatureId.McpReflection), Is.False, "reflection must stay off unless it is asked for");
        }

        [Test]
        public void EnableReflectionOnlyAlongsideTheServer()
        {
            FeaturesRegistry features = Resolve(AppArgsFlags.MCP, AppArgsFlags.MCP_REFLECTION);

            Assert.That(features.IsEnabled(FeatureId.McpServer), Is.True);
            Assert.That(features.IsEnabled(FeatureId.McpReflection), Is.True);
        }

        [Test]
        public void IgnoreReflectionWithoutTheServer()
        {
            // --mcp-reflection is inert on its own: the tools it unlocks only exist inside a running server.
            FeaturesRegistry features = Resolve(AppArgsFlags.MCP_REFLECTION);

            Assert.That(features.IsEnabled(FeatureId.McpServer), Is.False);
            Assert.That(features.IsEnabled(FeatureId.McpReflection), Is.False);
        }

        private static FeaturesRegistry Resolve(params string[] flags)
        {
            var args = new string[flags.Length];

            for (var i = 0; i < flags.Length; i++)
                args[i] = "--" + flags[i];

            return new FeaturesRegistry(new ApplicationParametersParser(false, args), false);
        }
    }
}
