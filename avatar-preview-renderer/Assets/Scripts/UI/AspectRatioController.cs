using System;
using Configurator;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class AspectRatioController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private ConfiguratorUIPresenter uiPresenter;
        [SerializeField] private ConfiguratorCameraController cameraController;

        [SerializeField] private float portraitMatchLimit = 1f;
        [SerializeField] private float portraitForceLimit = 2f;

        [SerializeField] private Vector2Int landscapeMatchResolution;
        [SerializeField] private Vector2Int portraitMatchResolution;
        [SerializeField] private Vector2Int portraitForceResolution;

        [SerializeField] private bool continuousUpdate;

        private Mode _currentMode = Mode.LandscapeMatch;
        private VisualElement _root;

        private void OnEnable()
        {
            _root = uiDocument.rootVisualElement.Q("root");
        }

        private void OnDisable()
        {
            // This will be reset the settings to original, used for UI Toolkit live reload
            TryUpdateMode(Mode.LandscapeMatch);
        }

        private void Update()
        {
            var width = Screen.width;
            var height = Screen.height;

            var aspectRatio = (float)width / height;

            Mode newMode;

            if (aspectRatio > portraitMatchLimit)
            {
                newMode = Mode.LandscapeMatch;
            }
            else if (aspectRatio > portraitForceLimit)
            {
                newMode = Mode.PortraitMatch;
            }
            else
            {
                newMode = Mode.PortraitForce;
            }

            TryUpdateMode(newMode);
        }

        private void TryUpdateMode(Mode newMode)
        {
            if (_currentMode == newMode && !continuousUpdate) return;

            Debug.Log($"Switching UI to {newMode}");

            switch (newMode)
            {
                case Mode.LandscapeMatch:
                    uiDocument.panelSettings.match = 1f;
                    uiDocument.panelSettings.referenceResolution = landscapeMatchResolution;
                    _root.EnableInClassList("portrait", false);
                    uiPresenter.SetUsingMobileMode(false);
                    cameraController.SetUsingMobileMode(false);
                    break;
                case Mode.PortraitMatch:
                    uiDocument.panelSettings.match = 0f;
                    uiDocument.panelSettings.referenceResolution = portraitMatchResolution;
                    _root.EnableInClassList("portrait", false);
                    uiPresenter.SetUsingMobileMode(false);
                    cameraController.SetUsingMobileMode(false);
                    break;
                case Mode.PortraitForce:
                    uiDocument.panelSettings.match = 0f;
                    uiDocument.panelSettings.referenceResolution = portraitForceResolution;
                    _root.EnableInClassList("portrait", true);
                    uiPresenter.SetUsingMobileMode(true);
                    cameraController.SetUsingMobileMode(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newMode), newMode, null);
            }

            _currentMode = newMode;
        }

        private void OnDestroy()
        {
            // Revert for editor
            uiDocument.panelSettings.match = 1f;
            uiDocument.panelSettings.referenceResolution = landscapeMatchResolution;
        }

        private enum Mode
        {
            LandscapeMatch,
            PortraitMatch,
            PortraitForce
        }
    }
}