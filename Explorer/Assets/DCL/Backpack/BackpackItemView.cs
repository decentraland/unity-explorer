using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action OnSelectItem;

        [field: SerializeField]
        public string ItemId { get; set; }

        [field: SerializeField]
        public GameObject HoverBackground { get; private set; }

        [field: SerializeField]
        public GameObject SelectedBackground { get; private set; }

        [field: SerializeField]
        public Button EquipButton { get; private set; }

        [field: SerializeField]
        public Button InfoButton { get; private set; }

        [field: SerializeField]
        public GameObject EquippedIcon { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverBackground.SetActive(true);
            EquipButton.gameObject.SetActive(!EquippedIcon.activeSelf);
            InfoButton.gameObject.SetActive(EquippedIcon.activeSelf);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverBackground.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnSelectItem?.Invoke();
        }
    }
}
