using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Systems;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Tests
{
    public class CharacterTriggerAreaHandlerSystemShould : UnitySystemTestBase<CharacterTriggerAreaHandlerSystem>
    {
        private Entity entity;
        private TransformComponent entityTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private GameObject fakeMainPlayerGO;
        private GameObject fakeMainPlayerAvatarGO;
        private GameObject fakeCharacterTriggerAreaGO;

        [SetUp]
        public void Setup()
        {
            var fakeMainPlayerGO = new GameObject();
            var fakeMainPlayerAvatarGO = new GameObject();
            var fakeCharacterTriggerAreaGO = new GameObject();

            var mainPlayerReferences = new MainPlayerReferences
            {
                MainPlayerAvatarBase = new MainPlayerAvatarBase(),
                MainPlayerTransform = new MainPlayerTransform(),
            };

            mainPlayerReferences.MainPlayerAvatarBase.SetAvatarBase(fakeMainPlayerAvatarGO.AddComponent<AvatarBase>());
            mainPlayerReferences.MainPlayerTransform.SetTransform(fakeMainPlayerGO.transform);

            // Setup system
            IComponentPoolsRegistry poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            IComponentPool<CharacterTriggerArea> characterTriggerAreaPool = Substitute.For<IComponentPool<CharacterTriggerArea>>();
            poolsRegistry.GetReferenceTypePool<CharacterTriggerArea>().Returns(characterTriggerAreaPool);
            characterTriggerAreaPool.Get().Returns(fakeCharacterTriggerAreaGO.AddComponent<CharacterTriggerArea>());

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            system = new CharacterTriggerAreaHandlerSystem(world, new ComponentPool<CharacterTriggerArea>(), mainPlayerReferences, sceneStateProvider);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            entityTransformComponent = AddTransformToEntity(entity);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(entityTransformComponent.Transform.gameObject);
            Object.DestroyImmediate(fakeMainPlayerGO);
            Object.DestroyImmediate(fakeMainPlayerAvatarGO);
            Object.DestroyImmediate(fakeCharacterTriggerAreaGO);
        }

        [Test]
        public void SetupMonoBehaviourCorrectly() { }

        [Test]
        public void UpdateMonoBehaviourCorrectly() { }

        [Test]
        public void TriggerEnterEventOnCharacterEnter() { }

        [Test]
        public void TriggerExitEventOnCharacterExit() { }

        [Test]
        public void ResetTriggerAreaEffectOnSceneLeave() { }

        [Test]
        public void HandleComponentRemoveCorrectly() { }

        [Test]
        public void HandleEntityDestructionCorrectly() { }

        [Test]
        public void DisableTriggerColliderWhenMainPlayerIsOutOfScene() { }

        [Test]
        public void WaitUntilAvatarBaseIsConfiguredBeforeActing() { }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void DiscriminateCharacterTypeCorrectly(bool onlyMainPlayer) { }
    }
}
