using UnityEngine;

namespace DCL.UI.Buttons
{
    public class SimpleHoverableButton: HoverableButton
    {
        [SerializeField] private GameObject _hoveredObject = null!;
        [SerializeField] private GameObject _unhoveredObject;
        [SerializeField] private GameObject _selectedObject;

        private bool selected;

        private void Start()
        {
            OnButtonHover += OnHover;
            OnButtonUnhover += OnUnhover;
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
            Button.interactable = !selected;

            _selectedObject.SetActive(selected);
            _unhoveredObject.SetActive(!selected);
        }

        private void OnUnhover()
        {
            if (selected) return;

            _hoveredObject.SetActive(false);
            _unhoveredObject.SetActive(true);
        }

        private void OnHover()
        {
            if (selected) return;

            _hoveredObject.SetActive(true);
            _unhoveredObject.SetActive(false);
        }
    }
}
