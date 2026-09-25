using DCL.AvatarRendering.AvatarShape.UnityInterface;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    /// <summary>
    ///     ResetState runs when a pooled avatar is released. The release may happen while the hierarchy is inactive (a character
    ///     preview whose container was deactivated mid-emote), so it has to write the prefab pose back explicitly and return the
    ///     rig to the prefab state for the next owner.
    /// </summary>
    public class AvatarBaseResetStateShould
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const string ANIMATOR_CONTROLLER_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Assets/Animator/CharacterAnimator.controller";
        private const float POSITION_TOLERANCE = 1e-4f;
        private const float ANGLE_TOLERANCE = 0.01f;

        private GameObject avatarGameObject = null!;
        private AvatarBase avatarBase = null!;

        private readonly List<Transform> prefabTransforms = new ();
        private readonly List<Vector3> prefabLocalPositions = new ();
        private readonly List<Quaternion> prefabLocalRotations = new ();
        private readonly List<Vector3> prefabLocalScales = new ();

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(prefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            // Instantiating an active prefab runs Awake, which captures the rest pose.
            avatarGameObject = Object.Instantiate(prefab);
            avatarBase = avatarGameObject.GetComponentInChildren<AvatarBase>();

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIMATOR_CONTROLLER_PATH);
            Assert.IsNotNull(controller, $"Could not load animator controller from {ANIMATOR_CONTROLLER_PATH}");
            avatarBase.AvatarAnimator.runtimeAnimatorController = controller;

            CapturePrefabPose();
            AvatarBaseTestRigWiring.WireMissingRigReferences(avatarBase);
        }

        [TearDown]
        public void TearDown()
        {
            if (avatarGameObject != null) Object.DestroyImmediate(avatarGameObject);

            prefabTransforms.Clear();
            prefabLocalPositions.Clear();
            prefabLocalRotations.Clear();
            prefabLocalScales.Clear();
        }

        [Test]
        public void RestorePrefabPoseWhenReleasedWhileInactive()
        {
            // Arrange
            PerturbPoseAndRig();
            avatarGameObject.SetActive(false);

            // Act
            avatarBase.ResetState();

            // Assert
            AssertPrefabPose();
            AssertRigAtPrefabState();

            avatarGameObject.SetActive(true);
            AssertPrefabPose();
        }

        [Test]
        public void RestorePrefabPoseWhenReleasedWhileActive()
        {
            // Arrange
            PerturbPoseAndRig();

            // Act
            avatarBase.ResetState();

            // Assert
            AssertPrefabPose();
            AssertRigAtPrefabState();
        }

        [Test]
        public void LeaveChildrenParentedAfterAwakeUntouched()
        {
            // Arrange
            var foreignChild = new GameObject("parented-after-awake");
            Transform parent = avatarBase.Armature;
            foreignChild.transform.SetParent(parent, false);
            var movedTo = new Vector3(1f, 2f, 3f);
            foreignChild.transform.localPosition = movedTo;

            // Act
            avatarBase.ResetState();

            // Assert
            Assert.AreSame(parent, foreignChild.transform.parent);
            Assert.AreEqual(0f, Vector3.Distance(movedTo, foreignChild.transform.localPosition), POSITION_TOLERANCE);
        }

        private void CapturePrefabPose()
        {
            foreach (Transform t in avatarBase.GetComponentsInChildren<Transform>(true))
            {
                if (t == avatarBase.transform) continue;

                prefabTransforms.Add(t);
                prefabLocalPositions.Add(t.localPosition);
                prefabLocalRotations.Add(t.localRotation);
                prefabLocalScales.Add(t.localScale);
            }

            Assert.Greater(prefabTransforms.Count, 60, "the test prefab is expected to carry the full skeleton");
        }

        private void PerturbPoseAndRig()
        {
            for (var i = 0; i < prefabTransforms.Count; i++)
            {
                Transform t = prefabTransforms[i];
                t.localPosition = prefabLocalPositions[i] + new Vector3(0.1f, -0.2f, 0.3f);
                t.localRotation = prefabLocalRotations[i] * Quaternion.Euler(35f, -20f, 50f);
                t.localScale = prefabLocalScales[i] * 1.5f;
            }

            avatarBase.RigBuilder.enabled = true;
            avatarBase.FeetIKRig.enabled = true;
            avatarBase.FeetIKRig.weight = 1f;
            avatarBase.HandsIKRig.weight = 1f;
            avatarBase.HeadIKRig.weight = 1f;
            avatarBase.TorsoIKRig.weight = 1f;
            avatarBase.CachePoseRig.weight = 1f;
            avatarBase.AdditiveBreathRig.weight = 1f;
            avatarBase.LeftLegIK.weight = 1f;
            avatarBase.RightLegIK.weight = 1f;
        }

        private void AssertPrefabPose()
        {
            for (var i = 0; i < prefabTransforms.Count; i++)
            {
                Transform t = prefabTransforms[i];
                Assert.AreEqual(0f, Vector3.Distance(prefabLocalPositions[i], t.localPosition), POSITION_TOLERANCE, $"{t.name} local position");
                Assert.AreEqual(0f, Quaternion.Angle(prefabLocalRotations[i], t.localRotation), ANGLE_TOLERANCE, $"{t.name} local rotation");
                Assert.AreEqual(0f, Vector3.Distance(prefabLocalScales[i], t.localScale), POSITION_TOLERANCE, $"{t.name} local scale");
            }
        }

        private void AssertRigAtPrefabState()
        {
            Assert.IsFalse(avatarBase.RigBuilder.enabled, "RigBuilder must go back to the prefab's disabled state");
            Assert.IsFalse(avatarBase.FeetIKRig.enabled);
            Assert.AreEqual(0f, avatarBase.FeetIKRig.weight);
            Assert.AreEqual(0f, avatarBase.HandsIKRig.weight);
            Assert.AreEqual(0f, avatarBase.HeadIKRig.weight);
            Assert.AreEqual(0f, avatarBase.TorsoIKRig.weight);
            Assert.AreEqual(0f, avatarBase.CachePoseRig.weight);
            Assert.AreEqual(0f, avatarBase.AdditiveBreathRig.weight);
            Assert.AreEqual(0f, avatarBase.LeftLegIK.weight);
            Assert.AreEqual(0f, avatarBase.RightLegIK.weight);
        }
    }
}
