namespace DCL.Settings
{
    public interface ISettingsDataStore
    {
        bool HasKey(string key);

        void SetToggleValue(string key, bool value, bool save = false);
        bool GetToggleValue(string key);

        void SetSliderValue(string key, float value, bool save = false);
        float GetSliderValue(string key);

        void SetDropdownValue(string key, int value, bool save = false);
        int GetDropdownValue(string key);

        void Save();
    }
}
