using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Minimap
{
    public class SceneRestrictionsController : IDisposable
    {
        private readonly SceneRestrictionsView restrictionsView;
        private readonly Dictionary<SceneRestrictions, bool> restrictionsRegistry = new();

        public SceneRestrictionsController(SceneRestrictionsView restrictionsView, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.restrictionsView = restrictionsView;

            restrictionsView.OnPointerEnterEvent += OnMouseEnter;
            restrictionsView.OnPointerExitEvent += OnMouseExit;
            sceneRestrictionBusController.SubscribeToSceneRestriction(ManageSceneRestrictions);
        }

        public void Dispose()
        {
            restrictionsView.OnPointerEnterEvent -= OnMouseEnter;
            restrictionsView.OnPointerExitEvent -= OnMouseExit;
        }

        private void OnMouseEnter()
        {
            Vector3 toastPosition = restrictionsView.toastRectTransform.anchoredPosition;
            toastPosition.x = restrictionsView.sceneRestrictionsIcon.transform.localPosition.x;
            restrictionsView.toastRectTransform.anchoredPosition = toastPosition;
            restrictionsView.toastCanvasGroup.DOFade(1f, 0.5f);
        }

        private void OnMouseExit() =>
            restrictionsView.toastCanvasGroup.DOFade(0f, 0.5f);

        private void ManageSceneRestrictions(ISceneRestriction sceneRestriction)
        {
            bool isRestrictionAdded = sceneRestriction.Action == SceneRestrictionsAction.APPLIED;

            GameObject textIndicator = sceneRestriction.Type switch
                                       {
                                           SceneRestrictions.CAMERA_LOCKED => restrictionsView.cameraLockedText.gameObject,
                                           SceneRestrictions.AVATAR_HIDDEN => restrictionsView.avatarHiddenText.gameObject,
                                           SceneRestrictions.AVATAR_MOVEMENTS_BLOCKED => restrictionsView.avatarMovementsText.gameObject,
                                           SceneRestrictions.PASSPORT_CANNOT_BE_OPENED => restrictionsView.passportBlockedText.gameObject,
                                           SceneRestrictions.EXPERIENCES_BLOCKED => restrictionsView.experiencesBlockedText.gameObject,
                                           _ => throw new ArgumentOutOfRangeException(),
                                       };

            textIndicator.SetActive(isRestrictionAdded);

            restrictionsRegistry[sceneRestriction.Type] = isRestrictionAdded;

            bool restrictionIconEnabled = RestrictionsRegistryHasAtLeastOneActive();
            restrictionsView.sceneRestrictionsIcon.gameObject.SetActive(restrictionIconEnabled);
            if (!restrictionIconEnabled)
                OnMouseExit();
        }

        private bool RestrictionsRegistryHasAtLeastOneActive()
        {
            foreach (bool flag in restrictionsRegistry.Values)
                if (flag)
                    return true;

            return false;
        }
    }
}
