using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.UI
{
    public struct ColorPickerPopupData
    {
        public Color InitialColor { get; set; }
        public List<Color> ColorPresets { get; set; }
        public Action<Color>? OnColorChanged { get; set; }
        public UniTaskCompletionSource? CloseTask { get; set; }
        public Vector2? Position { get; set; }
        public bool? EnableSaturationSlider { get; set; }
        public bool? EnableValueSlider { get; set; }
    }
}
