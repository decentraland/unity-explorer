using Cysharp.Threading.Tasks;
using DCL.Prefs;
using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Utility.Storage
{
    public static class PersistentSetting
    {
        public static PersistentSetting<bool> CreateBool(string key, bool defaultValue)
        {
            PersistentSetting<bool>.ApplyIfNot(
                static (key, defaultValue) => DCLPlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1,
                static (key, value) => DCLPlayerPrefs.SetInt(key, value ? 1 : 0)
            );

            return new PersistentSetting<bool>(key, defaultValue);
        }

        public static PersistentSetting<int> CreateInt(string key, int defaultValue)
        {
            PersistentSetting<int>.ApplyIfNot(
                static (key, defaultValue) => DCLPlayerPrefs.GetInt(key, defaultValue),
                static (key, value) => DCLPlayerPrefs.SetInt(key, value)
            );

            return new PersistentSetting<int>(key, defaultValue);
        }

        public static PersistentSetting<T> WithSetForceDefaultValue<T>(this PersistentSetting<T> persistentSetting) where T: IEquatable<T>
        {
            persistentSetting.Value = persistentSetting.defaultValue;
            return persistentSetting;
        }

        public static PersistentSetting<float> CreateFloat(string key, float defaultValue)
        {
            PersistentSetting<float>.ApplyIfNot(
                static (key, defaultValue) => DCLPlayerPrefs.GetFloat(key, defaultValue),
                static (key, value) => DCLPlayerPrefs.SetFloat(key, value)
            );

            return new PersistentSetting<float>(key, defaultValue);
        }

        public static PersistentSetting<T> CreateEnum<T>(string key, T defaultValue) where T: unmanaged, Enum, IEquatable<T>
        {
            PersistentSetting<T>.ApplyIfNot(
                static (key, defaultValue) => EnumUtils.FromInt<T>(DCLPlayerPrefs.GetInt(key, EnumUtils.ToInt(defaultValue))),
                static (key, value) => DCLPlayerPrefs.SetInt(key, EnumUtils.ToInt(value))
            );

            return new PersistentSetting<T>(key, defaultValue);
        }

        public static PersistentSetting<string> CreateString(string key, string defaultValue)
        {
            PersistentSetting<string>.ApplyIfNot(
                static (key, defaultValue) => DCLPlayerPrefs.GetString(key, defaultValue),
                static (key, value) => DCLPlayerPrefs.SetString(key, value)
            );

            return new PersistentSetting<string>(key, defaultValue);
        }

        public static PersistentSetting<Vector2Int> CreateVector2Int(string key, Vector2Int defaultValue = default)
        {
            PersistentSetting<Vector2Int>.ApplyIfNot(
                static (key, defaultVector) => new Vector2Int(
                    DCLPlayerPrefs.GetInt($"{key}_vector_x", defaultVector.x),
                    DCLPlayerPrefs.GetInt($"{key}_vector_y", defaultVector.y)
                ),
                static (key, value) =>
                {
                    DCLPlayerPrefs.SetInt($"{key}_vector_x", value.x);
                    DCLPlayerPrefs.SetInt($"{key}_vector_y", value.y);
                }
            );

            return new PersistentSetting<Vector2Int>(key, defaultValue);
        }

        public static PersistentSetting<Color> CreateColor(string key, Color defaultValue)
        {
            PersistentSetting<Color>.ApplyIfNot(
                static (key, defaultValue) =>
                {
                    string str = DCLPlayerPrefs.GetString(key, string.Empty)!;

                    if (string.IsNullOrEmpty(str))
                        return defaultValue;

                    return ColorUtility.TryParseHtmlString(str, out Color color) ? color : defaultValue;
                },
                static (key, value) => DCLPlayerPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(value)!)
            );

            return new PersistentSetting<Color>(key, defaultValue);
        }
    }

    /// <summary>
    ///     Value which is stored in DCLPlayerPrefs at runtime and can override values coming from presets
    /// </summary>
    public readonly struct PersistentSetting<T>
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
            get
            {
                EnsureMainThread();
                return getValue!(key, defaultValue)!;
            }

            set
            {
                EnsureMainThread();

                // It's for runtime only, in case it is used from the Editor it should not be saved anywhere
                if (!Application.isPlaying)
                    return;

                setValue!(key, value);
            }
        }

        public void ForceSave(T newValue)
        {
            EnsureMainThread();
            setValue!(key, newValue);
        }

        [Conditional("DEBUG")]
        private void EnsureMainThread()
        {
            if (PlayerLoopHelper.IsMainThread == false)
                throw new InvalidOperationException($"Cannot access PersistentSetting outside of the main thread, key: {key}, current thread: {Thread.CurrentThread.ManagedThreadId}, main thread: {PlayerLoopHelper.MainThreadId}");
        }
    }
}
