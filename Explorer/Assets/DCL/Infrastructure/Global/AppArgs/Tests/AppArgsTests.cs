using NUnit.Framework;
using System.Collections.Generic;

namespace Global.AppArgs.Tests
{
    public class AppArgsTest
    {
        [TearDown]
        public void TearDown()
        {
            // Reset the cached/overridden world whitelist so tests don't leak state into one another.
            DeepLinkAllowlist.SetWhitelistedWorlds(null);
            DeepLinkAllowlist.SetCustomBaseDomain(null);
        }

        [Test]
        public void DeepLinkSigninWithHostSegmentParsesSignin()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://open?signin=abc-123");
            Assert.AreEqual("abc-123", output.GetValueOrDefault(AppArgsFlags.SIGNIN), $"keys: {string.Join(", ", output.Keys)}");
        }

        [Test]
        public void DeepLinkSigninWithoutHostSegmentParsesSignin()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://?signin=abc-123");
            Assert.AreEqual("abc-123", output.GetValueOrDefault(AppArgsFlags.SIGNIN), $"keys: {string.Join(", ", output.Keys)}");
        }

        [Test]
        public void DeepLinkLegacyHostlessParamsUnaffectedByHostStripping()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://realm=http://127.0.0.1:8000&position=100,100");
            Assert.AreEqual("http://127.0.0.1:8000", output.GetValueOrDefault(AppArgsFlags.REALM), $"keys: {string.Join(", ", output.Keys)}");
            Assert.AreEqual("100,100", output.GetValueOrDefault(AppArgsFlags.POSITION), $"keys: {string.Join(", ", output.Keys)}");
        }

        [Test]
        public void DebugArgSuccessWithoutFlagTest()
        {
            // This succeeds because the Debug.isDebugBuild is always true when running tests
            IAppArgs args = new ApplicationParametersParser(false);
            Assert.True(args.HasDebugFlag(), $"flags in args: {string.Join(", ", args.Flags())}");
        }

        [Test]
        public void DebugArgFailTest()
        {
            IAppArgs args = new ApplicationParametersParser(false, "-debug");
            Assert.False(args.HasDebugFlag(false), $"flags in args: {string.Join(", ", args.Flags())}");
        }

        [Test]
        public void DebugArgContainsTest()
        {
            IAppArgs args = new ApplicationParametersParser(false, "--debug");
            Assert.True(args.HasDebugFlag(false), $"flags in args: {string.Join(", ", args.Flags())}");
        }

        [Test]
        public void DeepLinkDropsInternalFlags()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?creator-hub-bin-path=%5C%5Cattacker%5Cshare%5Cp.exe&launch-cdp-monitor-on-start&local-scene=true&comms-adapter=x&skip-auth-screen=true");

            Assert.IsFalse(output.ContainsKey("creator-hub-bin-path"), "creator-hub-bin-path must be dropped from deep links (not an app-arg; the Creator Hub path is resolved at runtime)");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START), "launch-cdp-monitor-on-start must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.COMMS_ADAPTER), "comms-adapter must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen must be dropped");
        }

        [Test]
        public void DeepLinkKeepsAllowlistedParams()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&position=10,20&community=abc&signin=id1&authRequestId=req1&force-open-backpack=true&spawnpoint=lobby&dclenv=zone");

            Assert.AreEqual("https://peer.decentraland.org", output.GetValueOrDefault(AppArgsFlags.REALM));
            Assert.AreEqual("10,20", output.GetValueOrDefault(AppArgsFlags.POSITION));
            Assert.AreEqual("abc", output.GetValueOrDefault(AppArgsFlags.COMMUNITY));
            Assert.AreEqual("id1", output.GetValueOrDefault(AppArgsFlags.SIGNIN));
            Assert.AreEqual("req1", output.GetValueOrDefault(AppArgsFlags.AUTH_REQUEST_ID));
            Assert.IsTrue(output.ContainsKey(AppArgsFlags.FORCE_OPEN_BACKPACK), "force-open-backpack must survive (shipped feature #9398)");
            Assert.AreEqual("lobby", output.GetValueOrDefault(AppArgsFlags.SPAWN_POINT), "spawnpoint must survive (named scene spawn point #9369)");
            Assert.AreEqual("zone", output.GetValueOrDefault(AppArgsFlags.ENVIRONMENT), "dclenv must survive for any realm: it is the only channel that carries the environment into a launched client");
        }

        [Test]
        public void DeepLinkKeepsEnvironmentForRealmlessLoginCallback()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://open?signin=id1&dclenv=zone&authRequestId=req1&local-scene=true&skip-auth-screen=true&multi-instance=true&scene-console=true");

            Assert.AreEqual("zone", output.GetValueOrDefault(AppArgsFlags.ENVIRONMENT), "dclenv must survive a realm-less login callback, otherwise the client falls back to the default environment");
            Assert.AreEqual("id1", output.GetValueOrDefault(AppArgsFlags.SIGNIN));
            Assert.AreEqual("req1", output.GetValueOrDefault(AppArgsFlags.AUTH_REQUEST_ID));
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must still require a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen must still require a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MULTIPLE_RUNNING_INSTANCES), "multi-instance must still require a loopback realm");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SCENE_CONSOLE), "scene-console must survive without a realm: it is not realm-gated");
        }

        [Test]
        public void DeepLinkKeepsSelfPreviewBuilderCollections()
        {
            // The documented creator flow carries no realm (docs/unreleased-wearables-emotes-preview.md), so this has
            // to survive on the always-permitted tier rather than the whitelisted-realm one.
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?position=100,100&self-preview-builder-collections=a2041268-189e-4cef-902d-70272aed077c");

            Assert.AreEqual("a2041268-189e-4cef-902d-70272aed077c", output.GetValueOrDefault(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS),
                "self-preview-builder-collections must survive without a realm; ids are GUID-validated before reaching a URL");
        }

        [Test]
        public void DeepLinkKeepsLocalSceneForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&position=100,100&local-scene=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene must survive for a loopback (local dev) realm");
        }

        [Test]
        public void DeepLinkDropsLocalSceneForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://evil.example&local-scene=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped for a non-loopback (remote) realm (SEC-020)");
        }

        [Test]
        public void DeepLinkKeepsSdkAndCreatorHubDevParamsForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&position=10,20&local-scene=true&hub=true&skip-auth-screen=true&landscape-terrain-enabled=true&multi-instance=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.DCL_EDITOR), "hub");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED), "landscape-terrain-enabled");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MULTIPLE_RUNNING_INSTANCES), "multi-instance");
        }

        [Test]
        public void DeepLinkDropsSdkAndCreatorHubDevParamsForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&local-scene=true&hub=true&skip-auth-screen=true&landscape-terrain-enabled=true&multi-instance=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.DCL_EDITOR), "hub must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED), "landscape-terrain-enabled must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MULTIPLE_RUNNING_INSTANCES), "multi-instance must be dropped for a remote realm");
        }

        [Test]
        public void DeepLinkDropsExecAndInfraParamsEvenForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&creator-hub-bin-path=x&launch-cdp-monitor-on-start=true&comms-adapter=y&optimized-assets-url=https://evil.example");

            Assert.IsFalse(output.ContainsKey("creator-hub-bin-path"), "creator-hub-bin-path must never be permitted (SEC-005), even for a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START), "launch-cdp-monitor-on-start must never be permitted, even for a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.COMMS_ADAPTER), "comms-adapter must never be permitted, even for a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.OPTIMIZED_ASSETS_URL), "optimized-assets-url must never be permitted, even for a loopback realm — it points the AB/LOD/registry endpoints at arbitrary infrastructure for the whole session; local-ab derives the base from the realm instead");
        }

        [Test]
        public void DeepLinkKeepsMcpForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&local-scene=true&mcp=true&mcp-port=8124");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MCP), "mcp must survive for a loopback (local dev) realm — sdk-commands forwards it into the preview deep link");
            Assert.AreEqual("8124", output.GetValueOrDefault(AppArgsFlags.MCP_PORT), "mcp-port must survive for a loopback (local dev) realm");
        }

        [Test]
        public void DeepLinkDropsMcpForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&mcp=true&mcp-port=8124");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MCP), "mcp must be dropped for a non-loopback (remote) realm — it starts an unauthenticated loopback control port");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MCP_PORT), "mcp-port must be dropped for a non-loopback (remote) realm (it implies mcp)");
        }

        [Test]
        public void DeepLinkDropsMcpWithoutRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters("decentraland://?mcp=true&mcp-port=8124");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MCP), "mcp must be dropped when the link carries no realm at all (drive-by link against the default realm)");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MCP_PORT), "mcp-port must be dropped when the link carries no realm at all");
        }

        // scene-console is always permitted: creators and QA need the scene log console against production realms and
        // worlds, and it unlocks no capability (read-only view of logs the scene already emits; the full debug panel
        // still needs the never-permitted `debug`).
        [TestCase("decentraland://?realm=http://127.0.0.1:8000&scene-console=true", TestName = "loopback realm")]
        [TestCase("decentraland://?realm=https://peer.decentraland.org&scene-console=true", TestName = "production catalyst realm")]
        [TestCase("decentraland://?realm=other-world.dcl.eth&scene-console=true", TestName = "non-whitelisted world realm")]
        [TestCase("decentraland://?scene-console=true", TestName = "no realm")]
        public void DeepLinkKeepsSceneConsoleForEveryRealm(string deepLink)
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(deepLink);

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SCENE_CONSOLE), "scene-console must survive for every realm");
        }

        [Test]
        public void DeepLinkKeepsDevParamsForWhitelistedWorldRealm()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });

            // Act
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=test-world.dcl.eth&local-scene=true&dclenv=zone&mcp=true&measure-loading-time=true");

            // Assert
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene must survive for a whitelisted world realm");
            Assert.AreEqual("zone", output.GetValueOrDefault(AppArgsFlags.ENVIRONMENT), "dclenv must survive for a whitelisted world realm");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MCP), "mcp must survive for a whitelisted world realm");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MEASURE_LOADING_TIME), "measure-loading-time must survive for a whitelisted world realm — the QA loading-benchmark flow targets a whitelisted world");
        }

        [Test]
        public void DeepLinkKeepsDevParamsForWhitelistedWorldContentServerUrlRealm()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });

            // Act
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://worlds-content-server.decentraland.org/world/test-world.dcl.eth&local-scene=true");

            // Assert
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene must survive when the realm URL resolves to a whitelisted world");
        }

        [Test]
        public void DeepLinkDropsDevParamsForNonWhitelistedWorldRealm()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });

            // Act
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=other-world.dcl.eth&local-scene=true&mcp=true");

            // Assert
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped for a world that is not whitelisted");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MCP), "mcp must be dropped for a world that is not whitelisted");
        }

        // IsRealmWhitelisted gates BOTH the whitelisted-realm dev params and skipping the realm-change consent prompt
        // (DeepLinkHandle), so its exact semantics are pinned here.
        [TestCase("http://127.0.0.1:8000", true, TestName = "loopback ip")]
        [TestCase("http://localhost:8000", true, TestName = "loopback name")]
        [TestCase("test-world.dcl.eth", true, TestName = "whitelisted world, short form")]
        [TestCase("TEST-WORLD.DCL.ETH", true, TestName = "whitelisted world, case insensitive")]
        [TestCase("https://worlds-content-server.decentraland.org/world/test-world.dcl.eth", true, TestName = "whitelisted world, full form")]
        [TestCase("other-world.dcl.eth", false, TestName = "world not whitelisted")]
        [TestCase("https://peer.decentraland.org", false, TestName = "remote catalyst realm")]
        [TestCase("https://evil.example/world/test-world.dcl.eth", false, TestName = "attacker host naming a whitelisted world must not inherit its trust")]
        [TestCase("https://worlds-content-server.decentraland.org@evil.example/world/test-world.dcl.eth", false, TestName = "userinfo cannot spoof the host")]
        [TestCase("https://decentraland.org.attacker.com/world/test-world.dcl.eth", false, TestName = "suffix-lookalike host is rejected")]
        [TestCase("https://worlds-content-server.decentraland.zone/world/test-world.dcl.eth", true, TestName = "zone worlds server")]
        [TestCase("file:///test-world.dcl.eth", false, TestName = "file:// reports IsLoopback but must not be trusted")]
        [TestCase("", false, TestName = "empty")]
        public void ClassifyRealmAsWhitelisted(string realm, bool expected)
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });

            // Act & Assert
            Assert.AreEqual(expected, DeepLinkAllowlist.IsRealmWhitelisted(realm));
        }

        [Test]
        public void TrustCustomBaseDomainHostForWhitelistedRealm()
        {
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });
            const string REALM = "https://worlds-content-server.interconnected.online/world/test-world.dcl.eth";

            DeepLinkAllowlist.SetCustomBaseDomain(null);
            Assert.IsFalse(DeepLinkAllowlist.IsRealmWhitelisted(REALM), "a non-decentraland host is untrusted without --base-domain");

            DeepLinkAllowlist.SetCustomBaseDomain("interconnected.online");
            Assert.IsTrue(DeepLinkAllowlist.IsRealmWhitelisted(REALM), "the --base-domain host is trusted like decentraland.* once set");
        }

        [Test]
        public void DeferDeepLinkUntilInitializeDeepLinksIsCalled()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(null);

            var args = ApplicationParametersParser.CreateDeferringDeepLinks(new[]
            {
                "--feature-flags-url", "https://feature-flags.decentraland.zone",
                "decentraland://?realm=http://127.0.0.1:8000&local-scene=true",
            });

            // Assert: CLI flags are available immediately, the deep link is not applied yet
            Assert.IsTrue(args.TryGetValue(AppArgsFlags.FeatureFlags.URL, out string? url) && url == "https://feature-flags.decentraland.zone");
            Assert.IsTrue(args.HasPendingDeepLink, "the deep link must stay pending until the whitelist is fetched");
            Assert.IsFalse(args.HasFlag(AppArgsFlags.REALM), "deep-link params must not be applied before InitializeDeepLinks");

            // Act
            args.InitializeDeepLinks();

            // Assert
            Assert.IsFalse(args.HasPendingDeepLink);
            Assert.AreEqual("http://127.0.0.1:8000", args.TryGetValue(AppArgsFlags.REALM, out string? realm) ? realm : null);
            Assert.AreEqual("true", args.TryGetValue(AppArgsFlags.LOCAL_SCENE, out string? localScene) ? localScene : null, "loopback realm keeps the whitelisted-realm params");

            // Act & Assert: idempotent
            args.InitializeDeepLinks();
            Assert.IsFalse(args.HasPendingDeepLink);
        }

        [Test]
        public void CaptureDeniedDeepLinkParamsWithoutApplyingThem()
        {
            // Arrange
            // Note: "debug" is unusable here — in the Editor the parser always injects it (ALWAYS_IN_EDITOR).
            var args = ApplicationParametersParser.CreateDeferringDeepLinks(new[]
            {
                "decentraland://?realm=https://peer.decentraland.org&position=1,2&autopilot=true&comms-adapter=wss://evil.example/ws",
            });

            // Act
            args.InitializeDeepLinks();

            // Assert: permitted params applied, denied ones captured (key and value) but NOT applied
            Assert.AreEqual("https://peer.decentraland.org", args.TryGetValue(AppArgsFlags.REALM, out string? realm) ? realm : null);
            Assert.IsFalse(args.HasFlag(AppArgsFlags.AUTOPILOT), "denied params must not reach the app args without consent");
            Assert.IsFalse(args.HasFlag(AppArgsFlags.COMMS_ADAPTER), "denied params must not reach the app args without consent");
            Assert.AreEqual("true", args.DeniedDeepLinkParams.GetValueOrDefault(AppArgsFlags.AUTOPILOT));
            Assert.AreEqual("wss://evil.example/ws", args.DeniedDeepLinkParams.GetValueOrDefault(AppArgsFlags.COMMS_ADAPTER));
        }

        [Test]
        public void ApplyDeniedDeepLinkParamsOnConsent()
        {
            // Arrange
            var args = ApplicationParametersParser.CreateDeferringDeepLinks(new[]
            {
                "decentraland://?realm=https://peer.decentraland.org&debug=true&skip-version-check=true",
            });

            args.InitializeDeepLinks();

            // Act
            args.ApplyDeniedDeepLinkParams();

            // Assert
            Assert.AreEqual("true", args.TryGetValue(AppArgsFlags.DEBUG, out string? debug) ? debug : null);
            Assert.AreEqual("true", args.TryGetValue(AppArgsFlags.SKIP_VERSION_CHECK, out string? skip) ? skip : null);
            Assert.IsEmpty(args.DeniedDeepLinkParams, "applied params must not stay pending");
        }

        [Test]
        public void ReportNoDeniedParamsForFullyAllowlistedDeepLink()
        {
            // Arrange
            var args = ApplicationParametersParser.CreateDeferringDeepLinks(new[]
            {
                "decentraland://?realm=http://127.0.0.1:8000&position=1,2&local-scene=true",
            });

            // Act
            args.InitializeDeepLinks();

            // Assert
            Assert.IsEmpty(args.DeniedDeepLinkParams, "a fully allowlisted deep link must not trigger the warning dialog");
        }

        [Test]
        public void NotWhitelistAnyWorldWithoutConfiguration()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(null);

            // Act & Assert
            Assert.IsFalse(DeepLinkAllowlist.IsRealmWhitelisted("test-world.dcl.eth"), "no configured worlds must mean loopback-only");
            Assert.IsTrue(DeepLinkAllowlist.IsRealmWhitelisted("http://127.0.0.1:8000"), "loopback is always whitelisted");
        }

        [Test]
        public void DeepLinkKeepsLocalAbForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&local-scene=true&local-ab=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_AB), "local-ab must survive for a loopback (local dev) realm — Creator Hub forwards it into the preview deep link");
        }

        [Test]
        public void DeepLinkDropsLocalAbForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&local-scene=true&local-ab=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_AB), "local-ab must be dropped for a non-loopback (remote) realm");
        }

        [Test]
        public void DeepLinkKeepsMeasureLoadingTimeForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&measure-loading-time=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MEASURE_LOADING_TIME), "measure-loading-time must survive for a loopback (local dev) realm");
        }

        [Test]
        public void DeepLinkDropsMeasureLoadingTimeForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&measure-loading-time=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MEASURE_LOADING_TIME), "measure-loading-time must be dropped for a non-whitelisted realm — it suppresses the sign-in screen and quits the client when no identity is cached");
        }
    }
}
