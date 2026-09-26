using System;
using UnityEngine;

namespace DCL.UI.ErrorPopup
{
    [Serializable]
    public class DebugErrorPopupData
    {
        [SerializeField] private Sprite? icon;
        [SerializeField] private string? title;
        [SerializeField] private string? description;

        public UIProperty<Sprite> Icon => icon.ToUIPropertyOrEmpty();
        public UIProperty<string> Title => title.ToUIPropertyOrDefault();
        public UIProperty<string> Description => description.ToUIPropertyOrDefault();

        public ErrorPopupData AsData() =>
            new (Icon, Title, Description);
    }

    public readonly struct ErrorPopupData
    {
        public static ErrorPopupData Default => new (UIProperty<Sprite>.Empty, UIProperty<string>.UseDefault, UIProperty<string>.UseDefault);

        public static ErrorPopupData FromDescription(string description) =>
            new (
                UIProperty<Sprite>.UseDefault,
                UIProperty<string>.UseDefault,
                UIProperty<string>.From(description)
            );

        public readonly UIProperty<Sprite> Icon;
        public readonly UIProperty<string> Title;
        public readonly UIProperty<string> Description;

        public ErrorPopupData(UIProperty<Sprite> icon, UIProperty<string> title, UIProperty<string> description)
        {
            Icon = icon;
            Title = title;
            Description = description;
        }
    }
}
