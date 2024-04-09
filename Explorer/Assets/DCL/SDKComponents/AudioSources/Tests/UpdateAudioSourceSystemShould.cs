using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Tests.Editor;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using static DCL.SDKComponents.AudioSources.Tests.AudioSourceTestsUtils;

namespace DCL.SDKComponents.AudioSources.Tests
{
    public class UpdateAudioSourceSystemShould : UnitySystemTestBase<UpdateAudioSourceSystem>
    {
        private AudioSourceComponent component;
        private Entity entity;


        public void SetUp()
        {
            system = CreateSystem(world);
            CreateComponent();
            CreateEntity();
            return;

            void CreateComponent()
            {
                component = new AudioSourceComponent(CreatePBAudioSource(), AssetPromise<AudioClip, GetAudioClipIntention>.Create(world, new GetAudioClipIntention(), PartitionComponent.TOP_PRIORITY));
            }

            void CreateEntity()
            {
                entity = world.Create(component, new PBAudioSource());
                AddTransformToEntity(entity);
            }
        }

        public static UpdateAudioSourceSystem CreateSystem(World world)
        {
            IComponentPoolsRegistry poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            IComponentPool<AudioSource> audioSourcesPool = Substitute.For<IComponentPool<AudioSource>>();

            poolsRegistry.GetReferenceTypePool<AudioSource>().Returns(audioSourcesPool);
            audioSourcesPool.Get().Returns(new GameObject().AddComponent<AudioSource>());

            IPerformanceBudget budgetProvider = Substitute.For<IPerformanceBudget>();
            budgetProvider.TrySpendBudget().Returns(true);

            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            IDereferencableCache<AudioClip, GetAudioClipIntention> cache = Substitute.For<IDereferencableCache<AudioClip, GetAudioClipIntention>>();
            return new UpdateAudioSourceSystem(world, ECSTestUtils.SceneDataSub(), sceneStateProvider, cache, poolsRegistry, budgetProvider, budgetProvider);
        }


        public void NotCreateAudioSourceIfClipNotFinishedLoading()
        {
            // Act
            system.Update(0);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipPromise, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource, Is.Null);
        }


        public void CreateAudioSourceFromResolvedPromise()
        {
            // Arrange
            world.Add(component.ClipPromise.Entity, new StreamableLoadingResult<AudioClip>(TestAudioClip));

            // Act
            system.Update(0);

            // Assert
            AudioSourceComponent afterUpdate = world.Get<AudioSourceComponent>(entity);
            Assert.That(afterUpdate.ClipPromise, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource, Is.Not.Null);
            Assert.That(afterUpdate.AudioSource.clip, Is.EqualTo(TestAudioClip));
        }
    }
}
