using DCL.EventsApi;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Events
{
    public class EventListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject offlineInteractionContainer;
        [SerializeField] private GameObject liveInteractionContainer;

        [SerializeField] private Button jumpInButton;
        [SerializeField] private Button liveShareButton;
        [SerializeField] private Button interestedButton;
        [SerializeField] private Button offlineShareButton;

        [Header("Event info")]
        [SerializeField] private TMP_Text eventTimeText;
        [SerializeField] private TMP_Text eventNameText;
        [SerializeField] private TMP_Text eventOnlineUsersText;
        [SerializeField] private TMP_Text eventInterestedUsersText;

        public event Action<EventDTO> JumpInButtonClicked;
        public event Action<EventDTO> InterestedButtonClicked;

        private EventDTO? eventData;

        private void Awake()
        {
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventData!.Value));
            interestedButton.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventData!.Value));
        }

        public void Configure(EventDTO data)
        {
            eventData = data;
        }

        public void OnPointerEnter(PointerEventData data)
        {
            offlineInteractionContainer.SetActive(!eventData!.Value.live);
            liveInteractionContainer.SetActive(eventData!.Value.live);
        }

        public void OnPointerExit(PointerEventData data)
        {
            offlineInteractionContainer.SetActive(false);
            liveInteractionContainer.SetActive(false);
        }
    }
}
