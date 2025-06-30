namespace DCL.Prefs
{
    internal interface IDCLPrefs
    {
        void SetString(string key, string value);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void SetBool(string key, bool value);

        string GetString(string key, string defaultValue);
        int GetInt(string key, int defaultValue);
        float GetFloat(string key, float defaultValue);
        bool GetBool(string key, bool defaultValue);

        bool HasKey(string key);
        void DeleteKey(string key);
        void DeleteAll();

        void Save();

    }
}
