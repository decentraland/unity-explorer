using System;
using Data;
using UI.Elements;
using UnityEngine.UIElements;

namespace Configurator.Views
{
    public class BodyShapePopupView : IRefreshableView
    {
        private readonly DCLDropdownElement _dropdown;
        private readonly PreviewButtonElement _maleBodyButton;
        private readonly PreviewButtonElement _femaleBodyButton;

        public event Action<BodyShape> BodyShapeSelected;
        
        private bool _autoClose;

        public BodyShapePopupView(DCLDropdownElement dropdown, VisualElement root)
        {
            _dropdown = dropdown;
            _maleBodyButton = root.Q<PreviewButtonElement>("MaleBodyButton");
            _femaleBodyButton = root.Q<PreviewButtonElement>("FemaleBodyButton");

            _maleBodyButton.Clicked += OnMaleBodyClicked;
            _femaleBodyButton.Clicked += OnFemaleBodyClicked;
        }

        public void SetBodyShape(BodyShape bodyShape)
        {
            _femaleBodyButton.Selected = bodyShape == BodyShape.Female;
            _maleBodyButton.Selected = bodyShape == BodyShape.Male;
        }

        private void OnFemaleBodyClicked()
        {
            _femaleBodyButton.Selected = true;
            _maleBodyButton.Selected = false;
            BodyShapeSelected!(BodyShape.Female);
            
            if(_autoClose) _dropdown.Open(false);
        }

        private void OnMaleBodyClicked()
        {
            _femaleBodyButton.Selected = false;
            _maleBodyButton.Selected = true;
            BodyShapeSelected!(BodyShape.Male);

            if (_autoClose) _dropdown.Open(false);
        }

        public void SetAutoClose(bool autoClose)
        {
            _autoClose = autoClose;
        }

        public object GetData()
        {
            return (_femaleBodyButton.Selected ? BodyShape.Female : BodyShape.Male, _dropdown.IsOpen);
        }

        public void SetData(object data)
        {
            var cast = ((BodyShape bodyShape, bool isOpen))data;

            SetBodyShape(cast.bodyShape);
            _dropdown.Open(cast.isOpen);
        }
    }
}