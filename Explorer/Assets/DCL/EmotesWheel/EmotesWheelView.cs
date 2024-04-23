using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.EmotesWheel
{
    public class EmotesWheelView : ViewBase, IView
    {
        public event Action? OnClose;

        [SerializeField]
        private Button[] closeButtons = null!;

        [field: SerializeField]
        public EmoteWheelSlotView[] Slots { get; set; } = null!;

        [field: SerializeField]
        public TMP_Text CurrentEmoteName { get; set; } = null!;

        private void Awake()
        {
            foreach (Button button in closeButtons)
                button.onClick.AddListener(() => OnClose?.Invoke());
        }
    }
}
