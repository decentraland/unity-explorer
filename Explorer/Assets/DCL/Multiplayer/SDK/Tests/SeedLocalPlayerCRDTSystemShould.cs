using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.Profiles;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Multiplayer.SDK.Tests
{
    public class LocalPlayerCRDTEntityHandlerSystemShould : UnitySystemTestBase<LocalPlayerCRDTEntityHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private World globalWorld;
        private Entity localPlayerEntity;
        private Entity persistentPlayerEntity;
        private PersistentEntities persistentEntities;
        private CharacterDataPropagationUtility characterDataPropagationUtility;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();

            localPlayerEntity = globalWorld.Create(
                Profile.NewRandomProfile(FAKE_USER_ID)
            );

            IComponentPool<SDKProfile> pool = Substitute.For<IComponentPool<SDKProfile>>();
            pool.Get().Returns(_ => new SDKProfile());
            characterDataPropagationUtility = new CharacterDataPropagationUtility(pool);

            persistentPlayerEntity = world.Create();
            persistentEntities = new PersistentEntities(persistentPlayerEntity, Entity.Null, Entity.Null, Entity.Null);

            system = new LocalPlayerCRDTEntityHandlerSystem(
                world, globalWorld, localPlayerEntity,
                characterDataPropagationUtility, persistentEntities);
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();
        }

        [Test]
        public void SeedPlayerCRDTEntityAndProfile()
        {
            system.Initialize();

            Assert.IsTrue(world.Has<PlayerSceneCRDTEntity>(persistentPlayerEntity));
            Assert.IsTrue(world.TryGet(persistentPlayerEntity, out PlayerSceneCRDTEntity crdtEntity));
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, crdtEntity.CRDTEntity.Id);

            Assert.IsTrue(world.TryGet(persistentPlayerEntity, out SDKProfile sdkProfile));
            Assert.AreEqual(FAKE_USER_ID, sdkProfile!.UserId);
        }

        [Test]
        public void NotSeedWhenPlayerHasNoProfile()
        {
            globalWorld.Remove<Profile>(localPlayerEntity);

            system.Initialize();

            Assert.IsFalse(world.Has<PlayerSceneCRDTEntity>(persistentPlayerEntity));
        }
    }
}
