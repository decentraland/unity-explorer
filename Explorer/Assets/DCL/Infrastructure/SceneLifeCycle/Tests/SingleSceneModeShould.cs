using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SingleScene;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SceneLifeCycle.Tests
{
    public class SingleSceneModeShould
    {
        private SingleSceneMode singleSceneMode = null!;

        [SetUp]
        public void SetUp()
        {
            singleSceneMode = new SingleSceneMode();
            singleSceneMode.SetActive(true);
        }

        [Test]
        public void NotHaveAnAnchorUntilOneIsSet()
        {

            Assert.That(singleSceneMode.HasAnchor, Is.False);
            Assert.That(singleSceneMode.IsRestricting, Is.False);
            Assert.That(singleSceneMode.IsAnchorScene(CreateDefinition(new Vector2Int(0, 0))), Is.False);
        }

        [Test]
        public void RecogniseEveryParcelOfTheAnchorScene()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));

            SceneDefinitionComponent anchorScene = CreateDefinition(new Vector2Int(10, 10), new Vector2Int(10, 11), new Vector2Int(11, 10));

            Assert.That(singleSceneMode.IsRestricting, Is.True);
            Assert.That(singleSceneMode.IsAnchorScene(anchorScene), Is.True);
            Assert.That(singleSceneMode.IsAnchorScene(CreateDefinition(new Vector2Int(12, 11))), Is.False);
        }

        [Test]
        public void StopRestrictingWhenTheAnchorIsCleared()
        {
            singleSceneMode.SetAnchor(new Vector2Int(10, 11));
            singleSceneMode.ClearAnchor();

            Assert.That(singleSceneMode.HasAnchor, Is.False);
            Assert.That(singleSceneMode.IsRestricting, Is.False);
        }

        private static SceneDefinitionComponent CreateDefinition(params Vector2Int[] parcels) =>
            SceneDefinitionComponentFactory.CreateFromDefinition(
                new SceneEntityDefinition
                {
                    metadata = new SceneMetadata
                    {
                        scene = new SceneMetadataScene { DecodedParcels = parcels },
                        runtimeVersion = "7",
                    },
                },
                new IpfsPath());
    }
}
