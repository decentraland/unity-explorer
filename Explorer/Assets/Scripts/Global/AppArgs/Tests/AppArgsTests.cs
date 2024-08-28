using NUnit.Framework;

namespace Global.AppArgs.Tests
{
    public class AppArgsTest
    {
        [Test]
        public void DebugArgFailTest()
        {
            IAppArgs args = new ApplicationParametersParser(false, "-debug");
            Assert.False(args.HasDebugFlag(), $"flags in args: {string.Join(", ", args.Flags())}");
        }

        [Test]
        public void DebugArgContainsTest()
        {
            IAppArgs args = new ApplicationParametersParser(false, "--debug");
            Assert.True(args.HasDebugFlag(), $"flags in args: {string.Join(", ", args.Flags())}");
        }
    }
}
