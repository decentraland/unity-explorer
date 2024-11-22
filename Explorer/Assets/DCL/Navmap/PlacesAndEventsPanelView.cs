using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class PlacesAndEventsPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public Button ExpandButton { get; private set; }

        [field: SerializeField]
        public Button CollapseButton { get; private set; }

        [field: SerializeField]
        public PlaceInfoPanelView PlaceInfoPanelView { get; private set; }

        [field: SerializeField]
        public EventInfoPanelView EventInfoPanelView { get; private set; }

        [field: SerializeField]
        public SearchFiltersView SearchFiltersView { get; private set; }

        public event Action? PointerEnter;
        public event Action? PointerExit;

        public void OnPointerEnter(PointerEventData eventData) =>
            PointerEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            PointerExit?.Invoke();
    }
}
