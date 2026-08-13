using CrdtEcsBridge.Components;
using DCL.Interaction.Utility;
using DCL.Utility.Exceptions;
using ECS.Prioritization.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class SceneInstanceDependenciesShould
    {
        [Test]
        public void ThrowManifestNotFoundExceptionWhenSdk7MainScriptMissing()
        {
            // ISceneData.Fake reports IsSdk7() == true while TryGetMainScriptUrl returns false with URLAddress.EMPTY,
            // reproducing the real no-main path: previously the discarded bool left SceneCodeUrl empty and the scene
            // failed later with an opaque source-fetch error; it must now fail fast at the source.
            Assert.Throws<ManifestNotFoundException>(() =>
                new SceneInstanceDependencies(
                    Substitute.For<ISDKComponentsRegistry>(),
                    Substitute.For<IEntityCollidersGlobalCache>(),
                    new ISceneData.Fake(),
                    Substitute.For<IJsApiPermissionsProvider>(),
                    Substitute.For<IPartitionComponent>(),
                    Substitute.For<IECSWorldFactory>(),
                    Substitute.For<ISceneEntityFactory>()));
        }
    }
}
