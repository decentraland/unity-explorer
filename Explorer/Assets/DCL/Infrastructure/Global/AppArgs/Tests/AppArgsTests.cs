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
                "decentraland://?realm=https://peer.decentraland.org&position=10,20&community=abc&signin=id1&authRequestId=req1&force-open-backpack=true&spawnpoint=lobby");

            Assert.AreEqual("https://peer.decentraland.org", output.GetValueOrDefault(AppArgsFlags.REALM));
            Assert.AreEqual("10,20", output.GetValueOrDefault(AppArgsFlags.POSITION));
            Assert.AreEqual("abc", output.GetValueOrDefault(AppArgsFlags.COMMUNITY));
            Assert.AreEqual("id1", output.GetValueOrDefault(AppArgsFlags.SIGNIN));
            Assert.AreEqual("req1", output.GetValueOrDefault(AppArgsFlags.AUTH_REQUEST_ID));
            Assert.IsTrue(output.ContainsKey(AppArgsFlags.FORCE_OPEN_BACKPACK), "force-open-backpack must survive (shipped feature #9398)");
            Assert.AreEqual("lobby", output.GetValueOrDefault(AppArgsFlags.SPAWN_POINT), "spawnpoint must survive (named scene spawn point #9369)");
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
                "decentraland://?realm=http://127.0.0.1:8000&position=10,20&local-scene=true&dclenv=zone&hub=true&skip-auth-screen=true&landscape-terrain-enabled=true&multi-instance=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene");
            Assert.AreEqual("zone", output.GetValueOrDefault(AppArgsFlags.ENVIRONMENT), "dclenv");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.DCL_EDITOR), "hub");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED), "landscape-terrain-enabled");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.MULTIPLE_RUNNING_INSTANCES), "multi-instance");
        }

        [Test]
        public void DeepLinkDropsSdkAndCreatorHubDevParamsForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&local-scene=true&dclenv=zone&hub=true&skip-auth-screen=true&landscape-terrain-enabled=true&multi-instance=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.ENVIRONMENT), "dclenv must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.DCL_EDITOR), "hub must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED), "landscape-terrain-enabled must be dropped for a remote realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.MULTIPLE_RUNNING_INSTANCES), "multi-instance must be dropped for a remote realm");
        }

        [Test]
        public void DeepLinkDropsExecAndInfraParamsEvenForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&creator-hub-bin-path=x&launch-cdp-monitor-on-start=true&comms-adapter=y");

            Assert.IsFalse(output.ContainsKey("creator-hub-bin-path"), "creator-hub-bin-path must never be permitted (SEC-005), even for a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START), "launch-cdp-monitor-on-start must never be permitted, even for a loopback realm");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.COMMS_ADAPTER), "comms-adapter must never be permitted, even for a loopback realm");
        }

        [Test]
        public void DeepLinkKeepsSceneConsoleForLoopbackRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=http://127.0.0.1:8000&scene-console=true");

            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SCENE_CONSOLE), "scene-console must survive for a loopback (local dev) realm");
        }

        [Test]
        public void DeepLinkDropsSceneConsoleForRemoteRealm()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&scene-console=true");

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SCENE_CONSOLE), "scene-console must be dropped for a non-whitelisted remote realm");
        }

        [Test]
        public void DeepLinkKeepsDevParamsForWhitelistedWorldRealm()
        {
            // Arrange
            DeepLinkAllowlist.SetWhitelistedWorlds(new[] { "test-world.dcl.eth" });

            // Act
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=test-world.dcl.eth&local-scene=true&dclenv=zone&scene-console=true");

            // Assert
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.LOCAL_SCENE), "local-scene must survive for a whitelisted world realm");
            Assert.AreEqual("zone", output.GetValueOrDefault(AppArgsFlags.ENVIRONMENT), "dclenv must survive for a whitelisted world realm");
            Assert.AreEqual("true", output.GetValueOrDefault(AppArgsFlags.SCENE_CONSOLE), "scene-console must survive for a whitelisted world realm");
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
                "decentraland://?realm=other-world.dcl.eth&local-scene=true&scene-console=true");

            // Assert
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped for a world that is not whitelisted");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SCENE_CONSOLE), "scene-console must be dropped for a world that is not whitelisted");
        }
    }
}
