using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.TestSuite;
using ECS.Unity.EngineInfo;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;

namespace ECS.Unity.Tests
{
    public class WriteEngineInfoSystemShould : UnitySystemTestBase<WriteEngineInfoSystem>
    {
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private SceneStateProvider sceneStateProvider = null!;
        private bool loadingScreenOn;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            sceneStateProvider = new SceneStateProvider();
            sceneStateProvider.Start(new SceneEngineStartInfo(DateTime.Now, 0));
            loadingScreenOn = false;

            system = new WriteEngineInfoSystem(world, sceneStateProvider, ecsToCRDTWriter, () => loadingScreenOn);
        }

        [Test]
        public void WriteSceneHiddenTrueWhileLoadingScreenIsOn()
        {
            // Arrange
            loadingScreenOn = true;

            // Act
            system.Update(0);

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: true);
        }

        [Test]
        public void WriteSceneHiddenFalseWhileLoadingScreenIsOff()
        {
            // Arrange
            loadingScreenOn = false;

            // Act
            system.Update(0);

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: false);
        }

        [Test]
        public void UpdateSceneHiddenWhenLoadingScreenStateFlips()
        {
            // Arrange
            loadingScreenOn = true;

            // Act
            system.Update(0);

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: true);
            ecsToCRDTWriter.ClearReceivedCalls();

            // Act
            loadingScreenOn = false;
            system.Update(0);

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: false);
            ecsToCRDTWriter.ClearReceivedCalls();

            // Act
            loadingScreenOn = true;
            system.Update(0);

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: true);
        }

        [Test]
        public void WriteEngineInfoOnInitialize()
        {
            // Arrange
            loadingScreenOn = true;

            // Act
            system.Initialize();

            // Assert
            AssertPutMessageReceived(expectedSceneHidden: true);
        }

        [Test]
        public void ApplySceneHiddenToComponent()
        {
            // Arrange
            loadingScreenOn = true;
            Action<PBEngineInfo, (ISceneStateProvider provider, bool sceneHidden)>? prepareMessage = null;
            (ISceneStateProvider provider, bool sceneHidden) capturedData = default;

            ecsToCRDTWriter.PutMessage(
                Arg.Do<Action<PBEngineInfo, (ISceneStateProvider provider, bool sceneHidden)>>(action => prepareMessage = action),
                Arg.Any<CRDTEntity>(),
                Arg.Do<(ISceneStateProvider provider, bool sceneHidden)>(data => capturedData = data));

            // Act
            system.Update(0);
            var component = new PBEngineInfo();
            prepareMessage!(component, capturedData);

            // Assert
            Assert.That(component.SceneHidden, Is.True);
            Assert.That(component.TickNumber, Is.EqualTo(sceneStateProvider.TickNumber));
        }

        private void AssertPutMessageReceived(bool expectedSceneHidden)
        {
            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBEngineInfo, (ISceneStateProvider provider, bool sceneHidden)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(ISceneStateProvider provider, bool sceneHidden)>(data =>
                                    data.provider == sceneStateProvider
                                    && data.sceneHidden == expectedSceneHidden));
        }
    }
}
