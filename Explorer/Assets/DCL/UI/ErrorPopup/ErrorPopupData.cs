using System;
using UnityEngine;

namespace DCL.UI.ErrorPopup
{
    [Serializable]
    public class ErrorPopupData
    {
        public static ErrorPopupData Empty = new ();

        [SerializeField] private Sprite? icon;
        [SerializeField] private string? title;
        [SerializeField] private string? description;

        public UIProperty<Sprite> Icon => icon.ToUIPropertyOrEmpty();
        public UIProperty<string> Title => title.ToUIPropertyOrDefault();
        public UIProperty<string> Description => description.ToUIPropertyOrDefault();
    }
}
