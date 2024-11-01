using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Minimap
{
    public class SceneRestrictionsController : IDisposable
    {
        private const float TOAST_X_POSITION_OFFSET_ICON_WIDTH_SCALER = 0.75f;

        private readonly ISceneRestrictionsView restrictionsView;
        private readonly Dictionary<SceneRestrictions, int> restrictionsRegistry = new();
        private readonly Dictionary<SceneRestrictions, GameObject> restrictionsGameObjects = new();
        private readonly Dictionary<SceneRestrictions, string> restrictionsTexts = new()
        {
            { SceneRestrictions.CAMERA_LOCKED, "• The camera is locked" },
            { SceneRestrictions.AVATAR_HIDDEN, "• The avatars are hidden" },
            { SceneRestrictions.AVATAR_MOVEMENTS_BLOCKED, "• Avatar movements are blocked" },
            { SceneRestrictions.PASSPORT_CANNOT_BE_OPENED, "• Passports can not be opened" },
            { SceneRestrictions.EXPERIENCES_BLOCKED, "• Experiences are blocked" },
        };

        public SceneRestrictionsController(ISceneRestrictionsView restrictionsView, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.restrictionsView = restrictionsView;

            restrictionsView.OnPointerEnterEvent += OnMouseEnter;
            restrictionsView.OnPointerExitEvent += OnMouseExit;
            sceneRestrictionBusController.SubscribeToSceneRestriction(ManageSceneRestrictions);

            foreach (SceneRestrictions restriction in Enum.GetValues(typeof(SceneRestrictions)))
            {
                restrictionsRegistry[restriction] = 0;

                GameObject restrictionsObject = GameObject.Instantiate(restrictionsView.RestrictionTextPrefab, restrictionsView.ToastTextParent.transform);
                restrictionsObject.GetComponent<TMP_Text>().SetText(restrictionsTexts[restriction]);
                restrictionsObject.SetActive(false);
                restrictionsObject.name = restriction.ToString();
                restrictionsGameObjects[restriction] = restrictionsObject;
            }
        }

        public void Dispose()
        {
            restrictionsView.OnPointerEnterEvent -= OnMouseEnter;
            restrictionsView.OnPointerExitEvent -= OnMouseExit;
        }

        private void OnMouseEnter()
        {
            restrictionsView.ToastCanvasGroup.gameObject.SetActive(true);
            Vector3 toastPosition = restrictionsView.ToastRectTransform.anchoredPosition;
            toastPosition.x = restrictionsView.SceneRestrictionsIcon.transform.localPosition.x - (restrictionsView.SceneRestrictionsIcon.rect.width * TOAST_X_POSITION_OFFSET_ICON_WIDTH_SCALER);
            restrictionsView.ToastRectTransform.anchoredPosition = toastPosition;
            restrictionsView.ToastCanvasGroup.DOFade(1f, restrictionsView.FadeTime);
        }

        private void OnMouseExit() =>
            restrictionsView.ToastCanvasGroup.DOFade(0f, restrictionsView.FadeTime).OnComplete(() => restrictionsView.ToastCanvasGroup.gameObject.SetActive(false));

        private void ManageSceneRestrictions(SceneRestriction sceneRestriction)
        {
            int currentRestrictionCounter = restrictionsRegistry[sceneRestriction.Type];

            currentRestrictionCounter += sceneRestriction.Action == SceneRestrictionsAction.APPLIED ? 1 : -1;
            currentRestrictionCounter = Mathf.Clamp(currentRestrictionCounter, 0, int.MaxValue);

            restrictionsRegistry[sceneRestriction.Type] = currentRestrictionCounter;

            restrictionsGameObjects[sceneRestriction.Type].SetActive(currentRestrictionCounter > 0);

            bool restrictionIconEnabled = RestrictionsRegistryHasAtLeastOneActive();
            restrictionsView.SceneRestrictionsIcon.gameObject.SetActive(restrictionIconEnabled);
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
