using Unity.Cinemachine;
using UnityEngine;

namespace Configurator
{
    public class ConfiguratorCameraController : MonoBehaviour
    {
        [SerializeField] private DragRotator dragRotator;
        [SerializeField] private ConfiguratorUIPresenter uiPresenter;
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private CinemachineCamera fullBodyCamera;
        [SerializeField] private CinemachineCamera headCamera;
        [SerializeField] private CinemachineCamera upperBodyCamera;
        [SerializeField] private CinemachineCamera lowerBodyCamera;
        [SerializeField] private CinemachineCamera centerStageCamera;
        [SerializeField] private CinemachinePositionComposer[] positionComposers;

        [SerializeField] private float mobileDistanceModifier = 2f;
        [SerializeField] private float mobileXRotation = 25f;

        private bool _hasZoomedOut;

        private float[] cameraDistances;

        private void Start()
        {
            fullBodyCamera.Prioritize();
            cinemachineBrain.ResetState();
            
            dragRotator.AllowVertical = false;
            dragRotator.EnableAutoRotate = false;
            dragRotator.LookAtCamera(false);

            uiPresenter.CategoryChanged += OnCategoryChanged;
            uiPresenter.CharacterAreaCenterChanged += OnCharacterAreaCenterChanged;
            uiPresenter.CharacterAreaZoom += OnCharacterAreaZoom;
            uiPresenter.Confirmed += OnConfirmed;

            cameraDistances = new float[positionComposers.Length];
            for (var i = 0; i < cameraDistances.Length; i++)
            {
                cameraDistances[i] = positionComposers[i].CameraDistance;
            }
        }

        public void SetUsingMobileMode(bool usingMobile)
        {
            for (var i = 0; i < cameraDistances.Length; i++)
            {
                var pc = positionComposers[i];
                pc.CameraDistance = usingMobile ? cameraDistances[i] * mobileDistanceModifier : cameraDistances[i];
                pc.transform.rotation = Quaternion.Euler(usingMobile ? mobileXRotation : 0f, 0f, 0f);
            }
        }

        private void OnConfirmed(bool open)
        {
            centerStageCamera.gameObject.SetActive(open);
        }

        private void OnCharacterAreaZoom(float delta)
        {
            switch (delta)
            {
                case > 0 when !fullBodyCamera.IsLive:
                    _hasZoomedOut = true;
                    fullBodyCamera.Prioritize();
                    break;
                case < 0 when _hasZoomedOut:
                    _hasZoomedOut = false;
                    fullBodyCamera.Prioritize();
                    break;
            }
        }

        private void OnCharacterAreaCenterChanged(Vector2 screenSpaceCenter)
        {
            foreach (var composer in positionComposers)
            {
                composer.Composition.ScreenPosition = screenSpaceCenter;
            }
        }

        private void OnCategoryChanged(string category)
        {
            _hasZoomedOut = false;

            switch (category)
            {
                case "mouth":
                case "eyewear":
                case "facial_hair":
                case "earring":
                case "hair":
                case "eyes":
                case "eyebrows":
                    headCamera.Prioritize();
                    break;
                case "lower_body":
                case "feet":
                    lowerBodyCamera.Prioritize();
                    break;
                case "hands_wear":
                case "upper_body":
                    upperBodyCamera.Prioritize();
                    break;
                default:
                    fullBodyCamera.Prioritize();
                    break;
            }

            dragRotator.LookAtCamera(true);
        }
    }
}