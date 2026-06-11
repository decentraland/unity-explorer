using DCL.Ipfs;
using DCL.LOD;
using DCL.SceneRunner.Scene;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using NUnit.Framework;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class VisualSceneStateResolverShould
    {
        private const int SDK7_LOD_THRESHOLD = 2;
        private const int UNLOAD_TOLERANCE = 1;
        private const string SDK7_RUNTIME = "7";

        private VisualSceneStateResolver resolver;

        [SetUp]
        public void SetUp()
        {
            ILODSettingsAsset lodSettingsAsset = ScriptableObject.CreateInstance<LODSettingsAsset>();
            lodSettingsAsset.SDK7LodThreshold = SDK7_LOD_THRESHOLD;
            lodSettingsAsset.UnloadTolerance = UNLOAD_TOLERANCE;

            resolver = new VisualSceneStateResolver(lodSettingsAsset);
        }

        [Test]
        public void ShowLODInWorldWhenDescriptorResolvedAndOverThreshold()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(SDK7_LOD_THRESHOLD), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.UNINITIALIZED, true, CreateResolvedDescriptor());

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_LOD));
        }

        [Test]
        public void ShowSceneInWorldWhenDescriptorResolvedAndUnderThreshold()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(SDK7_LOD_THRESHOLD - 1), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.UNINITIALIZED, true, CreateResolvedDescriptor());

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        [Test]
        public void ShowSceneInWorldWhenDescriptorIsNone()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(byte.MaxValue), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.UNINITIALIZED, true, ISSDescriptor.NONE);

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        [Test]
        public void ShowSceneInWorldWhenDescriptorIsUninitialized()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(byte.MaxValue), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.UNINITIALIZED, true, ISSDescriptor.CreateUninitialized());

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        [Test]
        public void ShowSceneInWorldForSDK6EvenWithResolvedDescriptor()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(byte.MaxValue), CreateSceneDefinition(null),
                VisualSceneState.UNINITIALIZED, true, CreateResolvedDescriptor());

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        [Test]
        public void ShowLODInVolatileRealmForSDK6()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(0), CreateSceneDefinition(null),
                VisualSceneState.UNINITIALIZED, false, ISSDescriptor.NONE);

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_LOD));
        }

        [Test]
        public void ShowLODInVolatileRealmWhenOverThreshold()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(SDK7_LOD_THRESHOLD), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.UNINITIALIZED, false, ISSDescriptor.NONE);

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_LOD));
        }

        [Test]
        public void KeepSceneShownWithinUnloadTolerance()
        {
            //Bucket is over the threshold, but the scene is already shown and within the unload tolerance
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(SDK7_LOD_THRESHOLD), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.SHOWING_SCENE, false, ISSDescriptor.NONE);

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        [Test]
        public void KeepSceneShownInWorldWithinUnloadTolerance()
        {
            VisualSceneState result = resolver.ResolveVisualSceneState(CreatePartition(SDK7_LOD_THRESHOLD), CreateSceneDefinition(SDK7_RUNTIME),
                VisualSceneState.SHOWING_SCENE, true, CreateResolvedDescriptor());

            Assert.That(result, Is.EqualTo(VisualSceneState.SHOWING_SCENE));
        }

        private static PartitionComponent CreatePartition(byte bucket) =>
            new ()
            {
                Bucket = bucket,
            };

        private static SceneDefinitionComponent CreateSceneDefinition(string runtimeVersion) =>
            SceneDefinitionComponentFactory.CreateFromDefinition(
                new SceneEntityDefinition
                {
                    metadata = new SceneMetadata
                    {
                        scene = new SceneMetadataScene
                            { DecodedParcels = new Vector2Int[1] },
                        runtimeVersion = runtimeVersion,
                    },
                },
                new IpfsPath());

        private static ISSDescriptor CreateResolvedDescriptor()
        {
            ISSDescriptor descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(null);
            return descriptor;
        }
    }
}
