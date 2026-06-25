using Cysharp.Threading.Tasks;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Minimap
{
    public class SceneRestrictionsController : IDisposable
    {
        private readonly ISceneRestrictionsView restrictionsView;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly Dictionary<SceneRestrictions, int> restrictionsRegistry = new();
        private readonly Dictionary<SceneRestrictions, GameObject> restrictionsGameObjects = new();
        private readonly Dictionary<SceneRestrictions, string> restrictionsTexts = new()
        {
            { SceneRestrictions.CAMERA_LOCKED, "• Camera locked" },
            { SceneRestrictions.AVATAR_HIDDEN, "• Avatars hidden" },
            { SceneRestrictions.AVATAR_MOVEMENTS_BLOCKED, "• Avatar movement disabled" },
            { SceneRestrictions.PASSPORT_CANNOT_BE_OPENED, "• User Options Menu disabled" },
            { SceneRestrictions.EXPERIENCES_BLOCKED, "• Experiences disabled" },
            { SceneRestrictions.SKYBOX_TIME_UI_BLOCKED, "• Day/Night controller disabled"},
            { SceneRestrictions.NEARBY_VOICE_CHAT_BLOCKED, "• Nearby voice disabled" },
        };

        public SceneRestrictionsController(ISceneRestrictionsView restrictionsView, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.restrictionsView = restrictionsView;
            this.sceneRestrictionBusController = sceneRestrictionBusController;

            foreach (SceneRestrictions restriction in Enum.GetValues(typeof(SceneRestrictions)))
            {
                restrictionsRegistry[restriction] = 0;

                GameObject restrictionsObject = Object.Instantiate(restrictionsView.RestrictionTextPrefab, restrictionsView.ToastTextParent.transform);
                restrictionsObject.SetActive(false);
                restrictionsObject.GetComponent<TMP_Text>().SetText(restrictionsTexts[restriction]);
                restrictionsObject.name = restriction.ToString();
                restrictionsGameObjects[restriction] = restrictionsObject;
            }

            sceneRestrictionBusController.SubscribeToSceneRestriction(ManageSceneRestrictions);
        }

        public void Dispose()
        {
            sceneRestrictionBusController.UnsubscribeToSceneRestriction(ManageSceneRestrictions);
        }

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
                restrictionsView.HideRestrictionToast();
            else
                restrictionsView.CycleToastAsync().Forget();
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
