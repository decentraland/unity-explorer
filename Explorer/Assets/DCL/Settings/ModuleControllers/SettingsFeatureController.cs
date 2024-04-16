using System;

namespace DCL.Settings.ModuleControllers
{
    public abstract class SettingsFeatureController : IDisposable
    {
        protected readonly SettingsDataStore settingsDataStore = new ();

        public abstract void Dispose();
    }
}
