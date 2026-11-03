using UnityEngine;

namespace DCL.UI.Buttons
{
    public class SimpleHoverableButton: HoverableButton, ISelectableButton
    {
        [SerializeField] private GameObject hoveredObject = null!;
        [SerializeField] private GameObject unhoveredObject = null!;
        [SerializeField] private GameObject selectedObject = null!;

        public bool IsSelected { get; private set; }

        private void Start()
        {
            OnButtonHover += OnHover;
            OnButtonUnhover += OnUnhover;
        }

        public void Select() =>
            SetSelected(true);

        public void Deselect() =>
            SetSelected(false);

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            selectedObject.SetActive(selected);
            unhoveredObject.SetActive(!selected);
            hoveredObject.SetActive(false);
        }

        private void OnUnhover()
        {
            if (IsSelected) return;

            hoveredObject.SetActive(false);
            unhoveredObject.SetActive(true);
        }

        private void OnHover()
        {
            if (IsSelected) return;

            hoveredObject.SetActive(true);
            unhoveredObject.SetActive(false);
        }
    }
}
