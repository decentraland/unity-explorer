namespace DCL.Settings
{
    public interface ISettingsDataStore
    {
        void SetToggleValue(string key, bool value, bool save = false);
        bool GetToggleValue(string key, bool defaultValue);

        void SetSliderValue(string key, float value, bool save = false);
        float GetSliderValue(string key, float defaultValue);

        void SetDropdownValue(string key, int value, bool save = false);
        int GetDropdownValue(string key, int defaultValue);

        void Save();
    }
}
