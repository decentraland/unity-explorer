using DCL.Settings.ModuleViews;
using System;
using System.Collections.Generic;

namespace DCL.Settings.ModuleControllers
{
    public abstract class SettingsFeatureController : IDisposable
    {
        protected readonly SettingsDataStore settingsDataStore = new ();

        internal ISettingsModuleView controllerView { get; private set; }

        public void SetView(ISettingsModuleView view) => controllerView = view;

        public void SetViewInteractable(bool interactable)
        {
            switch (controllerView)
            {
                case SettingsToggleModuleView toggle:
                    toggle.ToggleView.Toggle.interactable = interactable;
                    break;
                case SettingsDropdownModuleView dropdown:
                    dropdown.DropdownView.Dropdown.interactable = interactable;
                    break;
                case SettingsSliderModuleView slider:
                    slider.SliderView.Slider.interactable = interactable;
                    break;
            }
        }

        public virtual void OnAllControllersInstantiated(List<SettingsFeatureController> controllers){}

        public abstract void Dispose();
    }
}
