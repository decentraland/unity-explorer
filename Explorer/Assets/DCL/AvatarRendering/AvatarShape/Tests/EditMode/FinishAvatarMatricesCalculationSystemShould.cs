using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class FinishAvatarMatricesCalculationSystemShould : UnitySystemTestBase<FinishAvatarMatricesCalculationSystem>
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";

        private readonly List<GameObject> createdGameObjects = new ();

        private AvatarTransformMatrixJobWrapper jobWrapper = null!;
        private GameObject cameraGameObject = null!;

        [SetUp]
        public void SetUp()
        {
            cameraGameObject = new GameObject("TestCamera");
            createdGameObjects.Add(cameraGameObject);
            cameraGameObject.transform.position = Vector3.zero;

            // Looking down +Z, so anything at negative Z is behind it
            cameraGameObject.transform.rotation = Quaternion.identity;

            var testCamera = cameraGameObject.AddComponent<Camera>();
            testCamera.nearClipPlane = 0.1f;
            testCamera.farClipPlane = 1000f;
            world.Create(new CameraComponent(testCamera) { Mode = CameraMode.ThirdPerson });

            jobWrapper = new AvatarTransformMatrixJobWrapper();

            system = new FinishAvatarMatricesCalculationSystem(world, jobWrapper);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            jobWrapper.Dispose();

            foreach (GameObject createdGameObject in createdGameObjects)
                if (createdGameObject != null)
                    Object.DestroyImmediate(createdGameObject);

            createdGameObjects.Clear();
        }

        [Test]
        public void CullAnInWorldAvatarBehindTheCamera()
        {
            // Arrange
            Entity entity = CreateAvatarBehindCamera(isPreview: false);

            // Act
            RunFrame();

            // Assert
            Assert.IsFalse(world.Get<AvatarBase>(entity).AvatarAnimator.enabled,
                "an in-world avatar outside the frustum must be culled, which gates its Animator");
        }

        [Test]
        public void KeepThePreviewAvatarLiveWhereverThePlayerCameraLooks()
        {
            // The preview avatar is not culled, so it reaches ComputeSkinning, which reports on the
            // placeholder skinning component this fixture creates
            LogAssert.ignoreFailingMessages = true;

            // Arrange
            Entity entity = CreateAvatarBehindCamera(isPreview: true);

            // Act
            RunFrame();

            // Assert
            Assert.IsTrue(world.Get<AvatarBase>(entity).AvatarAnimator.enabled,
                "a preview avatar is drawn by its own camera, so the player camera must never cull it");
        }

        private void RunFrame()
        {
            jobWrapper.ScheduleBoneMatrixCalculation();
            system!.Update(0);
        }

        /// <summary>
        ///     Places an avatar 50 m behind the camera and registers it with the job so the calculation job
        ///     produces real world bounds for it, which is what the system tests against.
        /// </summary>
        private Entity CreateAvatarBehindCamera(bool isPreview)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(prefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            GameObject instance = Object.Instantiate(prefab);
            createdGameObjects.Add(instance);
            instance.transform.position = new Vector3(0, 0, -50);

            AvatarBase avatarBase = instance.GetComponentInChildren<AvatarBase>();
            Assert.IsNotNull(avatarBase, "AvatarBase component not found on test prefab");

            AvatarTransformMatrixComponent transformMatrix = AvatarTransformMatrixComponent.NewDefault();
            jobWrapper.RegisterAvatar(avatarBase, ref transformMatrix);
            jobWrapper.SetLocalBounds(ref transformMatrix, new Bounds(Vector3.zero, Vector3.one));

            var avatarShape = new AvatarShapeComponent("test-user-id", "TestUser") { IsPreview = isPreview };

            return world.Create(avatarShape, transformMatrix, avatarBase, default(AvatarCustomSkinningComponent));
        }
    }
}
