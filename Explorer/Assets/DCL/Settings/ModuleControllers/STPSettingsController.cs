using DCL.Settings.ModuleViews;
using DCL.Utilities;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class STPSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;

        public STPSettingsController(SettingsSliderModuleView viewInstance, UpscalingController upscalingController)
        {
            view = viewInstance;
            upscalingController.SetSliderModuleView(view);
        }

        public override void Dispose()
        {
        }
    }
}
