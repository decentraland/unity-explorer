using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.EventInfo
{
    public class EventInfoView: ViewBase, IView
    {
        private const string HOST_FORMAT = "Hosted by <b>{0}</b>";

        [SerializeField] private Button backgroundCloseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private EventInfoContextMenuConfiguration contextMenuSettings;
        [SerializeField] private ScrollRect scrollRect;
        [field: SerializeField] public WarningNotificationView SuccessNotificationView { get; private set; }
        [field: SerializeField] public WarningNotificationView ErrorNotificationView { get; private set; }

        [Header("Event Info")]
        [SerializeField] private ImageView eventImage;
        [SerializeField] private TMP_Text eventDate;
        [SerializeField] private TMP_Text eventName;
        [SerializeField] private TMP_Text hostName;
        [SerializeField] private ButtonWithSelectableStateView interestedButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button jumpInButton;
        [SerializeField] private Button permanentJumpInButton;
        [SerializeField] private TMP_Text eventDescription;
        [SerializeField] private TMP_Text eventSchedules;
        [SerializeField] private TMP_Text placeNameText;
        [SerializeField] private GameObject liveBadge;

        public event Action<IEventDTO>? InterestedButtonClicked;
        public event Action<IEventDTO>? JumpInButtonClicked;
        public event Action<IEventDTO>? EventShareButtonClicked;
        public event Action<IEventDTO>? EventCopyLinkButtonClicked;

        private readonly UniTask[] closeTasks = new UniTask[2];
        private readonly StringBuilder eventSchedulesStringBuilder = new ();
        private ImageController imageController;
        private IMVCManager mvcManager;
        private IEventDTO eventDTO;
        private GenericContextMenu contextMenu;

        private void Awake()
        {
            scrollRect.SetScrollSensitivityBasedOnPlatform();

            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventDTO));
            interestedButton.Button.onClick.AddListener(() => interestedButton.SetSelected(!interestedButton.Selected));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO));
            permanentJumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO));
            shareButton.onClick.AddListener(() => OpenContextMenu(shareButton.transform.position));

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ShareText, contextMenuSettings.ShareSprite, () => EventShareButtonClicked?.Invoke(eventDTO)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CopyLinkText, contextMenuSettings.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(eventDTO)));
        }

        private void OpenContextMenu(Vector2 position) =>
            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, position)));

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync();
            closeTasks[1] = closeButton.OnClickAsync();
            return closeTasks;
        }

        public void Configure(IMVCManager mvcManager,
            IWebRequestController webRequestController)
        {
            this.mvcManager = mvcManager;
            imageController ??= new ImageController(eventImage, webRequestController);
        }

        public void ConfigureEventData(IEventDTO eventData, PlacesData.PlaceInfo placeData)
        {
            eventDTO = eventData;

            ResetScrollPosition();

            imageController.RequestImage(eventData.Image);
            eventDate.text = EventUtilities.GetEventTimeText(eventData);
            eventName.text = eventData.Name;
            hostName.text = string.Format(HOST_FORMAT, eventData.User_name);
            UpdateInterestedButtonState();
            eventDescription.text = eventData.Description;
            jumpInButton.gameObject.SetActive(eventData.Live);
            interestedButton.gameObject.SetActive(!eventData.Live);
            liveBadge.SetActive(eventData.Live);

            placeNameText.text = eventData.World ? $"{placeData.title} ({placeData.world_name})" : $"{placeData.title} ({eventData.X},{eventData.Y})";

            eventSchedules.text = CalculateRecurrentSchedulesString(eventData);
        }

        private void ResetScrollPosition() =>
            scrollRect.verticalNormalizedPosition = 1f;

        private string CalculateRecurrentSchedulesString(IEventDTO eventData)
        {
            DateTime.TryParse(eventData.Next_start_at, null, DateTimeStyles.RoundtripKind, out DateTime nextStartAt);

            for (var i = 0; i < eventData.Recurrent_dates.Length; i++)
            {
                if (!DateTime.TryParse(eventData.Recurrent_dates[i], null, DateTimeStyles.RoundtripKind, out DateTime date)) continue;
                if (date < nextStartAt) continue;

                EventUtilities.FormatEventString(date, eventData.Duration, eventSchedulesStringBuilder);

                if (i < eventData.Recurrent_dates.Length - 1)
                    eventSchedulesStringBuilder.Append("\n");
            }

            var result = eventSchedulesStringBuilder.ToString();
            eventSchedulesStringBuilder.Clear();
            return result;
        }

        public void UpdateInterestedButtonState() =>
            interestedButton.SetSelected(eventDTO.Attending);
    }
}
