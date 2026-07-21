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

            Assert.IsFalse(output.ContainsKey(AppArgsFlags.CREATOR_HUB_BIN_PATH), "creator-hub-bin-path must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LAUNCH_CDP_MONITOR_ON_START), "launch-cdp-monitor-on-start must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.LOCAL_SCENE), "local-scene must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.COMMS_ADAPTER), "comms-adapter must be dropped");
            Assert.IsFalse(output.ContainsKey(AppArgsFlags.SKIP_AUTH_SCREEN), "skip-auth-screen must be dropped");
        }

        [Test]
        public void DeepLinkKeepsAllowlistedParams()
        {
            Dictionary<string, string> output = ApplicationParametersParser.ProcessDeepLinkParameters(
                "decentraland://?realm=https://peer.decentraland.org&position=10,20&community=abc&signin=id1&authRequestId=req1&force-open-backpack=true");

            Assert.AreEqual("https://peer.decentraland.org", output.GetValueOrDefault(AppArgsFlags.REALM));
            Assert.AreEqual("10,20", output.GetValueOrDefault(AppArgsFlags.POSITION));
            Assert.AreEqual("abc", output.GetValueOrDefault(AppArgsFlags.COMMUNITY));
            Assert.AreEqual("id1", output.GetValueOrDefault(AppArgsFlags.SIGNIN));
            Assert.AreEqual("req1", output.GetValueOrDefault(AppArgsFlags.AUTH_REQUEST_ID));
            Assert.IsTrue(output.ContainsKey(AppArgsFlags.FORCE_OPEN_BACKPACK), "force-open-backpack must survive (shipped feature #9398)");
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
    }
}
