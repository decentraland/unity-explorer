using NUnit.Framework;

namespace Global.AppArgs.Tests
{
    public class AppArgsTest
    {
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
