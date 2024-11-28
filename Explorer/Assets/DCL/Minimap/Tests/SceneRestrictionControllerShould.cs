using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Minimap.Tests
{
    public class SceneRestrictionControllerShould
    {
        private ISceneRestrictionsView sceneRestrictionsView;
        private ISceneRestrictionBusController sceneRestrictionBusController;
        private GameObject toastTextParent;
        private GameObject sceneRestrictionsIcon;

        private readonly Dictionary<SceneRestrictions, Transform> restrictionTexts = new();

        private SceneRestrictionsController sceneRestrictionsController;

        [SetUp]
        public void SetUp()
        {
            sceneRestrictionsView = Substitute.For<ISceneRestrictionsView>();
            sceneRestrictionBusController = new SceneRestrictionBusController.SceneRestrictionBus.SceneRestrictionBusController();
            sceneRestrictionsView.RestrictionTextPrefab.Returns(new GameObject("MockPrefab", typeof(TextMeshProUGUI)));

            toastTextParent = new GameObject("ToastTextParentMock", typeof(Transform));
            sceneRestrictionsView.ToastTextParent.Returns(toastTextParent);

            sceneRestrictionsIcon = new GameObject("IconMock", typeof(RectTransform));
            sceneRestrictionsView.SceneRestrictionsIcon.Returns(sceneRestrictionsIcon.GetComponent<RectTransform>());
            sceneRestrictionsIcon.SetActive(false);

            sceneRestrictionsController = new SceneRestrictionsController(sceneRestrictionsView, sceneRestrictionBusController);

            foreach (SceneRestrictions restriction in Enum.GetValues(typeof(SceneRestrictions)))
                restrictionTexts[restriction] = toastTextParent.transform.Find(restriction.ToString());
        }

        [TearDown]
        public void Dispose() =>
            sceneRestrictionsController.Dispose();

        [Test]
        public void ShowAllRestrictions()
        {
            //Assert
            foreach (SceneRestrictions restriction in Enum.GetValues(typeof(SceneRestrictions)))
            {
                Assert.IsNotNull(restrictionTexts[restriction]);
                Assert.IsFalse(restrictionTexts[restriction].gameObject.activeSelf);
            }
            Assert.IsFalse(sceneRestrictionsIcon.gameObject.activeSelf);


            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.APPLIED));

            //Assert
            Assert.IsTrue(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.REMOVED));

            //Assert
            Assert.IsFalse(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsFalse(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.APPLIED));
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));

            //Assert
            Assert.IsTrue(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.CAMERA_LOCKED].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.REMOVED));
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));

            //Assert
            Assert.IsFalse(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsFalse(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
            Assert.IsFalse(restrictionTexts[SceneRestrictions.CAMERA_LOCKED].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.APPLIED));
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));

            //Assert
            Assert.IsTrue(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.CAMERA_LOCKED].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));

            //Assert
            Assert.IsTrue(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.CAMERA_LOCKED].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));

            //Assert
            Assert.IsTrue(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsTrue(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
            Assert.IsFalse(restrictionTexts[SceneRestrictions.CAMERA_LOCKED].gameObject.activeSelf);

            //Act
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarHidden(SceneRestrictionsAction.REMOVED));

            //Assert
            Assert.IsFalse(sceneRestrictionsIcon.gameObject.activeSelf);
            Assert.IsFalse(restrictionTexts[SceneRestrictions.AVATAR_HIDDEN].gameObject.activeSelf);
        }
    }
}
