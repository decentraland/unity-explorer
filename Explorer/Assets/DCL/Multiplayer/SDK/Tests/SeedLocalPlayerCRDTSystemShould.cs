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

namespace DCL.Multiplayer.SDK.Tests
{
    public class LocalPlayerCRDTEntityHandlerSystemShould : UnitySystemTestBase<LocalPlayerCRDTEntityHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";
        private const string OTHER_USER_ID = "0xexample0000000000000000000000000000000001";

        private World globalWorld = null!;
        private Entity localPlayerEntity;
        private Entity persistentPlayerEntity;
        private PersistentEntities persistentEntities;
        private CharacterDataPropagationUtility characterDataPropagationUtility = null!;

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

            Assert.IsTrue(world.TryGet(persistentPlayerEntity, out SDKProfile? sdkProfile));
            Assert.AreEqual(FAKE_USER_ID, sdkProfile!.UserId);
        }

        [Test]
        public void NotSeedWhenPlayerHasNoProfile()
        {
            globalWorld.Remove<Profile>(localPlayerEntity);

            system.Initialize();

            Assert.IsFalse(world.Has<PlayerSceneCRDTEntity>(persistentPlayerEntity));
        }

        [Test]
        public void LeaveTalkingUnsetWithoutNearbyVoiceChat()
        {
            // Act
            system.Initialize();
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<SDKTalking>(persistentPlayerEntity));
        }

        [Test]
        public void MirrorLocalPlayerTalking()
        {
            // Arrange
            globalWorld.Add(localPlayerEntity, new TalkingComponent { IsTalking = true });

            // Act
            system.Initialize();
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Get<SDKTalking>(persistentPlayerEntity).IsTalking);

            // Act
            globalWorld.Get<TalkingComponent>(localPlayerEntity).IsTalking = false;
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Get<SDKTalking>(persistentPlayerEntity).IsTalking);
        }

        [Test]
        public void CopyReplacedProfileWithoutDirtyFlag()
        {
            // Arrange
            system.Initialize();
            Profile replacement = Profile.NewRandomProfile(OTHER_USER_ID);
            replacement.IsDirty = false;
            globalWorld.Set(localPlayerEntity, replacement);

            // Act
            system.Update(0);

            // Assert
            SDKProfile sdkProfile = world.Get<SDKProfile>(persistentPlayerEntity);
            Assert.AreEqual(OTHER_USER_ID, sdkProfile.UserId);
            Assert.IsTrue(sdkProfile.IsDirty);
        }

        [Test]
        public void NotCopyUnchangedProfile()
        {
            // Arrange
            system.Initialize();
            globalWorld.Get<Profile>(localPlayerEntity).IsDirty = false;
            SDKProfile sdkProfile = world.Get<SDKProfile>(persistentPlayerEntity);
            sdkProfile.IsDirty = false;

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(sdkProfile.IsDirty);
        }

        [Test]
        public void NotFailWhenProfileArrivesAfterInitializeWithoutDirtyFlag()
        {
            // Arrange
            Profile profile = globalWorld.Get<Profile>(localPlayerEntity);
            globalWorld.Remove<Profile>(localPlayerEntity);
            system.Initialize();
            profile.IsDirty = false;
            globalWorld.Add(localPlayerEntity, profile, new TalkingComponent { IsTalking = true });

            // Act & Assert
            Assert.DoesNotThrow(() => system.Update(0));
            Assert.IsTrue(world.Get<SDKTalking>(persistentPlayerEntity).IsTalking);
        }
    }
}
