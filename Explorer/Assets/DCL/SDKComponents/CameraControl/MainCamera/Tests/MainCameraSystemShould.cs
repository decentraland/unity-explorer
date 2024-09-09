using Arch.Core;
using Cinemachine;
using DCL.SDKComponents.CameraControl.MainCamera.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace DCL.SDKComponents.CameraControl.MainCamera.Tests
{
    public class MainCameraSystemShould : UnitySystemTestBase<MainCameraSystem>
    {
        [SetUp]
        public async void Setup()
        {

        }

        [TearDown]
        public void Teardown()
        {
            // poolsRegistry.Dispose();
            // Object.DestroyImmediate(virtualCamera.gameObject);
        }

        [Test]
        public void SetupMainCameraComponentCorrectly()
        {

        }

        [Test]
        public void UpdateMainCameraCorrectly()
        {

        }

        [Test]
        public void UpdateOnVirtualCameraLookAtUpdateCorrectly()
        {

        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {

        }

        [Test]
        public void HandleEntityDestructionCorrectly()
        {

        }
    }
}
