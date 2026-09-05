using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class FinishAvatarMatricesCalculationSystemShould : UnitySystemTestBase<FinishAvatarMatricesCalculationSystem>
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const float TOLERANCE = 0.001f;

        private static readonly Vector3 BEHIND_CAMERA = new (0, 0, -50);
        private static readonly Vector3 IN_FRONT_OF_CAMERA = new (0, 0, 10);

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
            // Arrange - start animated, so the assertion can only pass on a transition the system drove
            AvatarBase avatarBase = CreateAvatar(BEHIND_CAMERA, isPreview: false, out _);
            avatarBase.AvatarAnimator.enabled = true;

            // Act
            RunFrame();

            // Assert
            Assert.IsFalse(avatarBase.AvatarAnimator.enabled,
                "an in-world avatar outside the frustum must be culled, which gates its Animator");
        }

        [Test]
        public void NotCullAnInWorldAvatarInFrontOfTheCamera()
        {
            AllowTheSkinningDispatchToReport();

            // Arrange - start culled, so the assertion can only pass on a transition the system drove
            AvatarBase avatarBase = CreateAvatar(IN_FRONT_OF_CAMERA, isPreview: false, out _);
            avatarBase.AvatarAnimator.enabled = false;

            // Act
            RunFrame();

            // Assert
            Assert.IsTrue(avatarBase.AvatarAnimator.enabled,
                "an in-world avatar inside the frustum must not be culled");
        }

        [Test]
        public void KeepThePreviewAvatarLiveWhereverThePlayerCameraLooks()
        {
            AllowTheSkinningDispatchToReport();

            // Arrange - same position that culls the in-world avatar above, and start culled so the assertion
            // can only pass on a transition the system drove
            AvatarBase avatarBase = CreateAvatar(BEHIND_CAMERA, isPreview: true, out _);
            avatarBase.AvatarAnimator.enabled = false;

            // Act
            RunFrame();

            // Assert
            Assert.IsTrue(avatarBase.AvatarAnimator.enabled,
                "a preview avatar is drawn by its own camera, so the player camera must never cull it");
        }

        /// <summary>
        ///     Locks the Burst transform in BoneMatrixCalculationJob against the managed reference form, under a
        ///     rotation and translation, which is what would catch a transposed column in either of them.
        /// </summary>
        [Test]
        public void PlaceTheAvatarBoundsInTheWorldTheSameWayTheReferenceFormDoes()
        {
            // Arrange - behind the camera, so the avatar is culled and the dispatch never reports; the job
            // computes bounds regardless of the culling verdict
            var localBounds = new Bounds(new Vector3(0.1f, 0.9f, -0.2f), new Vector3(0.8f, 1.8f, 0.5f));

            AvatarBase avatarBase = CreateAvatar(BEHIND_CAMERA, isPreview: false, out AvatarTransformMatrixComponent transformMatrix,
                localBounds);

            avatarBase.transform.rotation = Quaternion.Euler(0, 37f, 0);

            // Act
            RunFrame();

            // Assert
            Assert.IsTrue(transformMatrix.IndexInGlobalJobArray.TryGetValue(out int index), "the avatar must hold a job slot");

            float3x2 fromJob = jobWrapper.RemoteAvatarsWorldBounds[index];
            Bounds expected = AvatarCustomSkinningComponent.NewWithLocalBounds(localBounds).ToWorldBounds(avatarBase.transform);

            Assert.That((float)fromJob.c0.x, Is.EqualTo(expected.center.x).Within(TOLERANCE));
            Assert.That((float)fromJob.c0.y, Is.EqualTo(expected.center.y).Within(TOLERANCE));
            Assert.That((float)fromJob.c0.z, Is.EqualTo(expected.center.z).Within(TOLERANCE));
            Assert.That((float)fromJob.c1.x, Is.EqualTo(expected.extents.x).Within(TOLERANCE));
            Assert.That((float)fromJob.c1.y, Is.EqualTo(expected.extents.y).Within(TOLERANCE));
            Assert.That((float)fromJob.c1.z, Is.EqualTo(expected.extents.z).Within(TOLERANCE));
        }

        /// <summary>
        ///     An avatar that is not culled reaches ComputeSkinning, which reports against the placeholder
        ///     skinning component this fixture creates rather than a real one backed by GPU buffers.
        /// </summary>
        private static void AllowTheSkinningDispatchToReport()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        private void RunFrame()
        {
            jobWrapper.ScheduleBoneMatrixCalculation();
            system!.Update(0);
        }

        /// <summary>
        ///     Places an avatar and registers it with the job, so the calculation job produces real world bounds
        ///     for it, which is what the system tests against.
        /// </summary>
        private AvatarBase CreateAvatar(Vector3 position, bool isPreview, out AvatarTransformMatrixComponent transformMatrix,
            Bounds? localBounds = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(prefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            GameObject instance = Object.Instantiate(prefab);
            createdGameObjects.Add(instance);
            instance.transform.position = position;

            AvatarBase avatarBase = instance.GetComponentInChildren<AvatarBase>();
            Assert.IsNotNull(avatarBase, "AvatarBase component not found on test prefab");

            Bounds bounds = localBounds ?? new Bounds(Vector3.zero, Vector3.one);

            transformMatrix = AvatarTransformMatrixComponent.NewDefault();
            jobWrapper.RegisterAvatar(avatarBase, ref transformMatrix);
            jobWrapper.SetLocalBounds(ref transformMatrix, bounds);

            var avatarShape = new AvatarShapeComponent("test-user-id", "TestUser") { IsPreview = isPreview };

            world.Create(avatarShape, transformMatrix, avatarBase, AvatarCustomSkinningComponent.NewWithLocalBounds(bounds));
            return avatarBase;
        }
    }
}
