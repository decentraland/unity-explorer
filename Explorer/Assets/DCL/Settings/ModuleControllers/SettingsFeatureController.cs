using DCL.Settings.ModuleViews;
using System;
using System.Collections.Generic;

namespace DCL.Settings.ModuleControllers
{
    public abstract class SettingsFeatureController : IDisposable
    {
        protected readonly SettingsDataStore settingsDataStore = new ();

        internal ISettingsModuleView controllerView { get; private set; }

        public void SetView(ISettingsModuleView view) =>
            controllerView = view;

        public void SetViewInteractable(bool interactable) =>
            controllerView?.SetInteractable(interactable);

        public virtual void OnAllControllersInstantiated(List<SettingsFeatureController> controllers){}

        public abstract void Dispose();
    }
}
