using DCL.Platforms;
using DCL.Quality.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MVC;

namespace DCL.Utilities
{
    public class UpscalingController
    {
        private const float STP_VALUE_FOR_UI_OPEN = 1f;
        private const float STP_HIGH_RESOLUTION_WINDOWS = 0.5f;
        private const float STP_HIGH_RESOLUTION_MAC = 0.5f;
        private const float STP_MID_RESOLUTION_MAC = 0.6f;
        private const float STP_MID_RESOLUTION_WINDOWS = 1f;

        private readonly float highResolutionPreset;
        private readonly float midResolutionPreset;
        private readonly IMVCManager mvcManager;

        private float savedUpscalingDuringUiOpen;
        private bool ignoreFirstResolutionChange;
        private int currentUiOpened;

        public UpscalingController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;

            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
            {
                highResolutionPreset = STP_HIGH_RESOLUTION_WINDOWS;
                midResolutionPreset = STP_MID_RESOLUTION_WINDOWS;
            }
            else
            {
                highResolutionPreset = STP_HIGH_RESOLUTION_MAC;
                midResolutionPreset = STP_MID_RESOLUTION_MAC;
            }

            mvcManager.OnViewShowed += OnUIOpened;
            mvcManager.OnViewClosed += OnUIClosed;
        }

        //Should always get in decimal form
        public void UpdateUpscaling(float newValue)
        {
            if (currentUiOpened > 0)
                savedUpscalingDuringUiOpen = newValue;
            else { SetUpscaling(newValue, UpscalingFilterSelection.FSR); }
        }

        private void OnUIClosed(IController controller)
        {
            if (ShouldTriggerUpscalerChange(controller))
            {
                currentUiOpened--;

                if (currentUiOpened == 0)
                    UpdateUpscaling(savedUpscalingDuringUiOpen);
            }
        }

        private void OnUIOpened(IController controller)
        {
            // Only trigger upscaler change for certain types of controllers
            if (ShouldTriggerUpscalerChange(controller))
            {
                // Only the first UI captures the user's scale; later ones would capture the forced value
                if (currentUiOpened == 0)
                {
                    savedUpscalingDuringUiOpen = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;
                    SetUpscaling(STP_VALUE_FOR_UI_OPEN, UpscalingFilterSelection.Auto);
                }

                currentUiOpened++;
            }
        }

        private void SetUpscaling(float renderScale, UpscalingFilterSelection filterSelection)
        {
            URPSettingsApplier.ApplyUpscaling(renderScale, filterSelection);
        }

        //This UIs should force an upscaling reset.
        private bool ShouldTriggerUpscalerChange(IController controller)
        {
            string controllerTypeName = controller.GetType().Name;
            return controllerTypeName.Contains("AuthenticationScreenController") ||
                   controllerTypeName.Contains("ExplorePanelController") ||
                   controllerTypeName.Contains("PassportController") ||
                   controllerTypeName.Contains("LobbyDocumentController") ||
                   controllerTypeName.Contains("BackpackModalController");
        }

        public void Dispose()
        {
            mvcManager.OnViewShowed -= OnUIOpened;
            mvcManager.OnViewClosed -= OnUIClosed;
        }
    }
}
