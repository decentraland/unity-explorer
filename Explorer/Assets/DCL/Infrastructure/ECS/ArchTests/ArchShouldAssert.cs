using Arch.Core;
using DCL.Diagnostics;
using NUnit.Framework;

#if DEBUG_ARCH

namespace ECS.ArchTests
{
    public class ArchShouldAssert
    {
        private World world;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void Add()
        {
            var e = world.Create("TEST");

            Assert.Throws<DebugTraceListener.DiagnosticsException>(() => world.Add(e, "REPEAT"));
        }

        [Test]
        public void Remove()
        {
            var e = world.Create("TEST");

            Assert.DoesNotThrow(() => world.Remove<string>(e));
            Assert.Throws<DebugTraceListener.DiagnosticsException>(() => world.Remove<string>(e));
        }
    }
}

#endif
