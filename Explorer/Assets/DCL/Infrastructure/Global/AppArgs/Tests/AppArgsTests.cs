using NUnit.Framework;
using System.Collections.Generic;

namespace Global.AppArgs.Tests
{
    public class AppArgsTest
    {
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
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SCENE_CONSOLE), "scene-console must stay dropped for every realm");
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
    }
}
