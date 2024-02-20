using System;
using UnityEngine;
using Utility;

namespace DCL.Quality
{
    internal static class PersistentSetting
    {
        public static PersistentSetting<bool> CreateBool(string key, bool defaultValue)
        {
            PersistentSetting<bool>.getValue ??= static (key, defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
            ;
            PersistentSetting<bool>.setValue ??= static (key, value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

            return new PersistentSetting<bool>(key, defaultValue);
        }

        public static PersistentSetting<int> CreateInt(string key, int defaultValue)
        {
            PersistentSetting<int>.getValue ??= static (key, defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
            PersistentSetting<int>.setValue ??= static (key, value) => PlayerPrefs.SetInt(key, value);

            return new PersistentSetting<int>(key, defaultValue);
        }

        public static PersistentSetting<float> CreateFloat(string key, float defaultValue)
        {
            PersistentSetting<float>.getValue ??= static (key, defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
            PersistentSetting<float>.setValue ??= static (key, value) => PlayerPrefs.SetFloat(key, value);

            return new PersistentSetting<float>(key, defaultValue);
        }

        public static PersistentSetting<T> CreateEnum<T>(string key, T defaultValue) where T: unmanaged, Enum
        {
            PersistentSetting<T>.getValue ??= static (key, defaultValue) => EnumUtils.FromInt<T>(PlayerPrefs.GetInt(key, EnumUtils.ToInt(defaultValue)));
            PersistentSetting<T>.setValue ??= static (key, value) => PlayerPrefs.SetInt(key, EnumUtils.ToInt(value));

            return new PersistentSetting<T>(key, defaultValue);
        }

        public static PersistentSetting<string> CreateString(string key, string defaultValue)
        {
            PersistentSetting<string>.getValue ??= static (key, defaultValue) => PlayerPrefs.GetString(key, defaultValue);
            PersistentSetting<string>.setValue ??= static (key, value) => PlayerPrefs.SetString(key, value);

            return new PersistentSetting<string>(key, defaultValue);
        }

        public static PersistentSetting<Color> CreateColor(string key, Color defaultValue)
        {
            PersistentSetting<Color>.getValue ??= static (key, defaultValue) =>
            {
                string? str = PlayerPrefs.GetString(key, string.Empty);

                if (string.IsNullOrEmpty(str))
                    return defaultValue;

                return ColorUtility.TryParseHtmlString(str, out Color color) ? color : defaultValue;
            };

            PersistentSetting<Color>.setValue ??= static (key, value) => PlayerPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(value));

            return new PersistentSetting<Color>(key, defaultValue);
        }
    }

    /// <summary>
    ///     Value which is stored in PlayerPrefs at runtime and can override values coming from presets
    /// </summary>
    public readonly struct PersistentSetting<T>
    {
        internal static Func<string, T, T>? getValue;
        internal static Action<string, T>? setValue;
        private readonly T defaultValue;

        private readonly string key;

        public PersistentSetting(string key, T defaultValue)
        {
            this.key = key;
            this.defaultValue = defaultValue;
            Value = getValue!(key, defaultValue);
        }

        public T Value
        {
            get
            {
                // It's for runtime only, in case it is used from the Editor it should not be saved anywhere
                if (Application.isPlaying)
                    return getValue!(key, defaultValue);

                return defaultValue;
            }

            set
            {
                if (!Application.isPlaying)
                    return;

                setValue!(key, value);
            }
        }
    }
}
