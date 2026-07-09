using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Character.CharacterMotion;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.ECSComponents;
using DCL.SDKComponents.PhysicsImpulse.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using Vector3 = UnityEngine.Vector3;

namespace DCL.SDKComponents.PhysicsImpulse.Tests
{
    /// <summary>
    ///     Covers the multi-scene contract of external forces: several worlds can be "current" at once
    ///     (parcel scene + Portable Experiences), and each world may only affect its own contribution
    ///     to the effective force on the global player.
    /// </summary>
    public class SDKExternalPhysicsSystemsShould : UnitySystemTestBase<SDKExternalPhysicsSystems>
    {
        private static readonly Vector3 FORCE_A = new (0f, 80f, 0f);
        private static readonly Vector3 FORCE_B = new (5f, 0f, 0f);

        private World globalWorld;
        private Entity playerEntity;
        private World worldB;
        private SDKExternalPhysicsSystems systemB;
        private ICharacterControllerSettings settings;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            playerEntity = globalWorld.Create(new CharacterRigidTransform());

            settings = Substitute.For<ICharacterControllerSettings>();
            settings.CharacterMass.Returns(1f);

            system = new SDKExternalPhysicsSystems(world, globalWorld, playerEntity, CurrentSceneStateProvider());

            worldB = World.Create();
            systemB = new SDKExternalPhysicsSystems(worldB, globalWorld, playerEntity, CurrentSceneStateProvider());
        }

        protected override void OnTearDown()
        {
            systemB.Dispose();
            worldB.Dispose();
            globalWorld.Dispose();
        }

        [Test]
        public void KeepForceWhenAnotherCurrentWorldWithoutForceUpdatesLast()
        {
            // Arrange - the PX repro: world B is also "current" but has no force components
            AddPlayerForceEntity(world, FORCE_A);

            // Act
            system.Update(0);
            systemB.Update(0);

            // Assert
            Assert.AreEqual(FORCE_A, RunApplyExternalForce().ExternalForce);
        }

        [Test]
        public void KeepForeignContributionWhenOtherWorldFinalizes()
        {
            // Arrange
            AddPlayerForceEntity(world, FORCE_A);
            system.Update(0);

            // Act - world B unloads (e.g. a PX is disposed) while A's force is active
            systemB.FinalizeComponents(default);

            // Assert
            Assert.AreEqual(FORCE_A, RunApplyExternalForce().ExternalForce);
        }

        [Test]
        public void RemoveOnlyOwnContributionWhenSceneStopsBeingCurrent()
        {
            // Arrange
            AddPlayerForceEntity(world, FORCE_A);
            AddPlayerForceEntity(worldB, FORCE_B);
            system.Update(0);
            systemB.Update(0);

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            Assert.AreEqual(FORCE_B, RunApplyExternalForce().ExternalForce);
        }

        [Test]
        public void RemoveOwnContributionWhenForceComponentIsRemoved()
        {
            // Arrange
            Entity forceEntity = AddPlayerForceEntity(world, FORCE_A);
            system.Update(0);
            Assert.AreEqual(FORCE_A, RunApplyExternalForce().ExternalForce);

            // Act
            world.Remove<PBPhysicsCombinedForce>(forceEntity);
            system.Update(0);

            // Assert
            Assert.AreEqual(Vector3.zero, RunApplyExternalForce().ExternalForce);
        }

        [Test]
        public void SumForcesFromAllCurrentWorlds()
        {
            // Arrange
            AddPlayerForceEntity(world, FORCE_A);
            AddPlayerForceEntity(worldB, FORCE_B);

            // Act
            system.Update(0);
            systemB.Update(0);

            // Assert
            Assert.AreEqual(FORCE_A + FORCE_B, RunApplyExternalForce().ExternalForce);
        }

        [Test]
        public void LeaveNoResidualAccelerationAfterAllContributionsRemoved()
        {
            // Arrange
            Entity forceEntityA = AddPlayerForceEntity(world, FORCE_A);
            Entity forceEntityB = AddPlayerForceEntity(worldB, FORCE_B);
            system.Update(0);
            systemB.Update(0);
            Assert.AreNotEqual(Vector3.zero, RunApplyExternalForce().ExternalAcceleration);

            // Act
            world.Remove<PBPhysicsCombinedForce>(forceEntityA);
            worldB.Remove<PBPhysicsCombinedForce>(forceEntityB);
            system.Update(0);
            systemB.Update(0);

            // Assert
            CharacterRigidTransform rigidTransform = RunApplyExternalForce();
            Assert.AreEqual(Vector3.zero, rigidTransform.ExternalForce);
            Assert.AreEqual(Vector3.zero, rigidTransform.ExternalAcceleration);
        }

        private static ISceneStateProvider CurrentSceneStateProvider()
        {
            ISceneStateProvider provider = Substitute.For<ISceneStateProvider>();
            provider.IsCurrent.Returns(true);
            return provider;
        }

        private static Entity AddPlayerForceEntity(World sceneWorld, Vector3 force) =>
            sceneWorld.Create(
                new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY),
                new PBPhysicsCombinedForce { Vector = new Decentraland.Common.Vector3 { X = force.x, Y = force.y, Z = force.z } });

        // Runs the consumer side (CalculateCharacterVelocitySystem -> ApplyExternalForce.Execute)
        // to observe the effective force/acceleration derived from what the scene systems wrote.
        private CharacterRigidTransform RunApplyExternalForce()
        {
            CharacterRigidTransform rigidTransform = globalWorld.Get<CharacterRigidTransform>(playerEntity);
            ApplyExternalForce.Execute(settings, ref rigidTransform, new GlideState(), 0.02f);
            return rigidTransform;
        }
    }
}
