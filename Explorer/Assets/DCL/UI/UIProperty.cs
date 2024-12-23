using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public struct UIProperty<T>
    {
        public readonly T Value;
        public readonly UIPropertyType Type;

        private UIProperty(T value, UIPropertyType type)
        {
            Value = value;
            Type = type;
        }

        public static UIProperty<T> UseDefault => new (default!, UIPropertyType.UseDefault);

        public static UIProperty<T> Empty => new (default!, UIPropertyType.Empty);

        public static UIProperty<T> From(T value) =>
            new (value, UIPropertyType.Value);
    }

    public enum UIPropertyType
    {
        /// <summary>
        /// Uses default value of the view
        /// </summary>
        UseDefault,
        /// <summary>
        /// Explicit empty value
        /// </summary>
        Empty,
        /// <summary>
        /// Explicit content value
        /// </summary>
        Value,
    }

    public static class UIPropertyExtensions
    {
        public static UIProperty<T> ToUIPropertyOrDefault<T>(this T? value) =>
            value == null ? UIProperty<T>.UseDefault : UIProperty<T>.From(value);

        public static UIProperty<T> ToUIPropertyOrEmpty<T>(this T? value) =>
            value == null ? UIProperty<T>.Empty : UIProperty<T>.From(value);

        public static void Apply(this Image image, UIProperty<Sprite> property, Sprite defaultSprite)
        {
            switch (property.Type)
            {
                case UIPropertyType.UseDefault:
                    image.sprite = defaultSprite;
                    image.color = Color.white;
                    break;
                case UIPropertyType.Empty:
                    image.color = Color.clear;
                    break;
                case UIPropertyType.Value:
                    image.sprite = property.Value;
                    image.color = Color.white;
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public static void Apply(this TMP_Text text, UIProperty<string> property, string defaultText) =>
            text.text = property.Type switch
                        {
                            UIPropertyType.UseDefault => defaultText,
                            UIPropertyType.Empty => string.Empty,
                            UIPropertyType.Value => property.Value,
                            _ => throw new ArgumentOutOfRangeException(),
                        };
    }
}
