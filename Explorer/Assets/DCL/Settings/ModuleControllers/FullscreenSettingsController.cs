using DCL.Settings.ModuleViews;
using Plugins.NativeWindowManager;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class FullscreenSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;

        public FullscreenSettingsController(SettingsToggleModuleView view)
        {
            this.view = view;

            view.ToggleView.Toggle.isOn = NativeWindowManager.FullScreenEnabled;
            view.ToggleView.Toggle.onValueChanged.AddListener(SetWindowModeSettingsOnValueChanged);

            view.SetInteractable(!Application.isEditor);
        }

        private void SetWindowModeSettingsOnValueChanged(bool isOn)
        {
            NativeWindowManager.FullScreenEnabled = isOn;
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveListener(SetWindowModeSettingsOnValueChanged);
        }
    }
}
