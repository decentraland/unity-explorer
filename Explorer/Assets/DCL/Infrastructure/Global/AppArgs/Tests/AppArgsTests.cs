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
    }
}
