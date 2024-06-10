using System;
using UnityEngine;

namespace Utility.Storage
{
    public static class PersistentSetting
    {
        public static PersistentSetting<bool> CreateBool(string key, bool defaultValue)
        {
            PersistentSetting<bool>.ApplyIfNot(
                static (key, defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1,
                static (key, value) => PlayerPrefs.SetInt(key, value ? 1 : 0)
            );

            return new PersistentSetting<bool>(key, defaultValue);
        }

        public static PersistentSetting<int> CreateInt(string key, int defaultValue)
        {
            PersistentSetting<int>.ApplyIfNot(
                static (key, defaultValue) => PlayerPrefs.GetInt(key, defaultValue),
                static (key, value) => PlayerPrefs.SetInt(key, value)
            );

            return new PersistentSetting<int>(key, defaultValue);
        }

        public static PersistentSetting<T> WithSetForceDefaultValue<T>(this PersistentSetting<T> persistentSetting) where T : IEquatable<T>
        {
            persistentSetting.Value = persistentSetting.defaultValue;
            return persistentSetting;
        }

        public static PersistentSetting<float> CreateFloat(string key, float defaultValue)
        {
            PersistentSetting<float>.ApplyIfNot(
                static (key, defaultValue) => PlayerPrefs.GetFloat(key, defaultValue),
                static (key, value) => PlayerPrefs.SetFloat(key, value)
            );

            return new PersistentSetting<float>(key, defaultValue);
        }

        public static PersistentSetting<T> CreateEnum<T>(string key, T defaultValue) where T: unmanaged, Enum, IEquatable<T>
        {
            PersistentSetting<T>.ApplyIfNot(
                static (key, defaultValue) => EnumUtils.FromInt<T>(PlayerPrefs.GetInt(key, EnumUtils.ToInt(defaultValue))),
                static (key, value) => PlayerPrefs.SetInt(key, EnumUtils.ToInt(value))
            );

            return new PersistentSetting<T>(key, defaultValue);
        }

        public static PersistentSetting<string> CreateString(string key, string defaultValue)
        {
            PersistentSetting<string>.ApplyIfNot(
                static (key, defaultValue) => PlayerPrefs.GetString(key, defaultValue),
                static (key, value) => PlayerPrefs.SetString(key, value)
            );

            return new PersistentSetting<string>(key, defaultValue);
        }

        public static PersistentSetting<Vector2Int> CreateVector2Int(string key, Vector2Int defaultValue = default)
        {
            PersistentSetting<Vector2Int>.ApplyIfNot(
                static (key, defaultVector) => new Vector2Int(
                    PlayerPrefs.GetInt($"{key}_vector_x", defaultVector.x),
                    PlayerPrefs.GetInt($"{key}_vector_y", defaultVector.y)
                ),
                static (key, value) =>
                {
                    PlayerPrefs.SetInt($"{key}_vector_x", value.x);
                    PlayerPrefs.SetInt($"{key}_vector_y", value.y);
                }
            );

            return new PersistentSetting<Vector2Int>(key, defaultValue);
        }

        public static PersistentSetting<Color> CreateColor(string key, Color defaultValue)
        {
            PersistentSetting<Color>.ApplyIfNot(
                static (key, defaultValue) =>
                {
                    string str = PlayerPrefs.GetString(key, string.Empty)!;

                    if (string.IsNullOrEmpty(str))
                        return defaultValue;

                    return ColorUtility.TryParseHtmlString(str, out Color color) ? color : defaultValue;
                },
                static (key, value) => PlayerPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(value)!)
            );

            return new PersistentSetting<Color>(key, defaultValue);
        }
    }

    public interface ISetting<T>
    {
        T Value { get; set; }
    }

    /// <summary>
    ///     Value which is stored in PlayerPrefs at runtime and can override values coming from presets
    /// </summary>
    public readonly struct PersistentSetting<T> : ISetting<T> where T : IEquatable<T>
    {
        private static Func<string, T, T>? getValue;
        private static Action<string, T>? setValue;
        internal readonly T defaultValue;

        private readonly string key;

        public PersistentSetting(string key, T defaultValue)
        {
            this.key = key;
            this.defaultValue = defaultValue;
            Value = getValue!(key, defaultValue)!;
        }

        public static void ApplyIfNot(Func<string, T, T>? getValueFunc, Action<string, T>? setValueFunc)
        {
            getValue ??= getValueFunc;
            setValue ??= setValueFunc;
        }

        public T Value
        {
            get => getValue!(key, defaultValue)!;

            set
            {
                // It's for runtime only, in case it is used from the Editor it should not be saved anywhere
                if (!Application.isPlaying)
                    return;

                setValue!(key, value);
            }
        }

        public CachedSetting<T, PersistentSetting<T>> WithCached() =>
            new (this);
    }

    public struct CachedSetting<T, TK> : ISetting<T> where TK: ISetting<T> where T: IEquatable<T>
    {
        private TK origin;
        private T? cache;

        public CachedSetting(TK origin) : this()
        {
            this.origin = origin;
        }

        public T Value
        {
            get
            {
                if (cache == null || cache.Equals(default(T)!))
                    cache = origin.Value;

                return cache;
            }

            set
            {
                origin.Value = value;
                cache = default(T?);
            }
        }
    }
}
