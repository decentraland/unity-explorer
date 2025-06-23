namespace DCL.Prefs
{
    internal interface IDCLPrefs
    {
        void SetString(string key, string value);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);

        string GetString(string key, string defaultValue);
        int GetInt(string key, int defaultValue);
        float GetFloat(string key, float defaultValue);

        bool HasKey(string key);
        void DeleteKey(string key);
        void DeleteAll();

        void Save();

    }
}
