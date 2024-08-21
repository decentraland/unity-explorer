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

        public Sprite? Icon => icon;
        public string? Title => title;
        public string? Description => description;
    }
}
