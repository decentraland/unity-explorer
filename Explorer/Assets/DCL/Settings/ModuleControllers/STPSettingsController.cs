using DCL.Settings.ModuleViews;
using DCL.Utilities;
using System;

namespace DCL.Settings.ModuleControllers
{
    public class STPSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;

        public STPSettingsController(SettingsSliderModuleView viewInstance, STPController stpController)
        {
            view = viewInstance;
            stpController.SetSliderModuleView(view);
        }

        public override void Dispose()
        {
        }
    }
}
