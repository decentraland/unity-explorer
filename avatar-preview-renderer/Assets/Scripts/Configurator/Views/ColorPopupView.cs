using System;
using UI.Elements;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace Configurator.Views
{
    public class ColorPopupView : IRefreshableView
    {
        public event Action<Color> ColorSelected;

        private readonly DCLDropdownElement _dropdown;
        private readonly VisualElement _icon;
        private readonly VisualElement _container;
        private readonly Label _title;

        private Color[] _colors;

        private bool _autoClose;

        public ColorPopupView(DCLDropdownElement dropdown, VisualElement root, VisualElement icon)
        {
            _dropdown = dropdown;
            _icon = icon;
            _container = root.Q("PresetsContainer");
            _title = root.Q<Label>("Title");
        }

        public void SetTitle(string title)
        {
            _title.text = $"<font-weight=600>{title}";
        }

        public void SetColors(Color[] colors)
        {
            _colors = colors;
            _container.Clear();

            foreach (var color in colors)
            {
                var preset = new VisualElement();
                preset.AddToClassList("popup-color__preset");
                preset.AddToClassList("dcl-clickable");
                preset.style.backgroundColor = color;
                preset.AddManipulator(new AudioClickable(() => OnColorSelected(color)));

                _container.Add(preset);
            }
        }

        public void SetSelectedColor(Color color)
        {
            _icon.style.backgroundColor = color;

            foreach (var preset in _container.Children())
            {
                preset.EnableInClassList("popup-color__preset--selected", preset.style.backgroundColor.value == color);
            }
        }

        public void SetAutoClose(bool autoClose)
        {
            _autoClose = autoClose;
        }

        private void OnColorSelected(Color color)
        {
            SetSelectedColor(color);
            ColorSelected!(color);

            if (_autoClose) _dropdown.Open(false);
        }

        public object GetData()
        {
            return (_colors, _icon.style.backgroundColor.value, _dropdown.IsOpen);
        }

        public void SetData(object data)
        {
            var cast = ((Color[] colors, Color selectedColor, bool isOpen))data;
            _icon.style.backgroundColor = cast.selectedColor;
            SetColors(cast.colors);
            _dropdown.Open(cast.isOpen);
        }
    }
}