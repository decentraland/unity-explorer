using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Multiplayer.SDK.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WritePlayerIdentityDataSystemShould : UnitySystemTestBase<WritePlayerIdentityDataSystem>
    {
        [SetUp]
        public void Setup()
        {
            IECSToCRDTWriter ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new WritePlayerIdentityDataSystem(world, ecsToCRDTWriter);
        }

        [TearDown]
        public void TearDown() { }

        [Test]
        public void Test() { }
    }
}
