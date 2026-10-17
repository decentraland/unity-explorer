using CrdtEcsBridge.Components;
using DCL.Interaction.Utility;
using DCL.Utility.Types;
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
        public void FailWhenSdk7MainScriptMissing()
        {
            // A missing SDK7 main script must fail at creation, not later as an opaque source-fetch error (#9598).
            Result<SceneInstanceDependencies> result = SceneInstanceDependencies.New(
                Substitute.For<ISDKComponentsRegistry>(),
                Substitute.For<IEntityCollidersGlobalCache>(),
                new ISceneData.Fake(),
                Substitute.For<IJsApiPermissionsProvider>(),
                Substitute.For<IPartitionComponent>(),
                Substitute.For<IECSWorldFactory>(),
                Substitute.For<ISceneEntityFactory>());

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found in the content manifest"));
        }
    }
}
