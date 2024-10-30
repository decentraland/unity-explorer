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
        private const float TOAST_X_POSITION_OFFSET_ICON_WIDTH_SCALER = 0.75f;

        private readonly SceneRestrictionsView restrictionsView;
        private readonly Dictionary<SceneRestrictions, int> restrictionsRegistry = new();

        public SceneRestrictionsController(SceneRestrictionsView restrictionsView, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.restrictionsView = restrictionsView;

            restrictionsView.OnPointerEnterEvent += OnMouseEnter;
            restrictionsView.OnPointerExitEvent += OnMouseExit;
            sceneRestrictionBusController.SubscribeToSceneRestriction(ManageSceneRestrictions);

            foreach (SceneRestrictions restriction in Enum.GetValues(typeof(SceneRestrictions)))
                restrictionsRegistry[restriction] = 0;
        }

        public void Dispose()
        {
            restrictionsView.OnPointerEnterEvent -= OnMouseEnter;
            restrictionsView.OnPointerExitEvent -= OnMouseExit;
        }

        private void OnMouseEnter()
        {
            Vector3 toastPosition = restrictionsView.toastRectTransform.anchoredPosition;
            toastPosition.x = restrictionsView.sceneRestrictionsIcon.transform.localPosition.x - (restrictionsView.sceneRestrictionsIcon.rect.width * TOAST_X_POSITION_OFFSET_ICON_WIDTH_SCALER);
            restrictionsView.toastRectTransform.anchoredPosition = toastPosition;
            restrictionsView.toastCanvasGroup.DOFade(1f, restrictionsView.fadeTime);
        }

        private void OnMouseExit() =>
            restrictionsView.toastCanvasGroup.DOFade(0f, restrictionsView.fadeTime);

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

            int currentRestrictionCounter = restrictionsRegistry[sceneRestriction.Type];
            currentRestrictionCounter += isRestrictionAdded ? 1 : -1;
            currentRestrictionCounter = Mathf.Clamp(currentRestrictionCounter, 0, int.MaxValue);
            restrictionsRegistry[sceneRestriction.Type] = currentRestrictionCounter;

            textIndicator.SetActive(currentRestrictionCounter > 0);

            bool restrictionIconEnabled = RestrictionsRegistryHasAtLeastOneActive();
            restrictionsView.sceneRestrictionsIcon.gameObject.SetActive(restrictionIconEnabled);
            if (!restrictionIconEnabled)
                OnMouseExit();
        }

        private bool RestrictionsRegistryHasAtLeastOneActive()
        {
            foreach (int counter in restrictionsRegistry.Values)
                if (counter > 0)
                    return true;

            return false;
        }
    }
}
