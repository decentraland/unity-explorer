using MVC;
using System;
using TMPro;
using UnityEngine;

namespace DCL.EmotesWheel
{
    public class EmotesWheelView : ViewBase, IView
    {
        public event Action? OnClose;

        [field: SerializeField]
        public EmoteWheelSlotView[] Slots { get; set; } = null!;

        [field: SerializeField]
        public TMP_Text CurrentEmoteName { get; set; } = null!;
    }
}
