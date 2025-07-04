using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Text;
using System.Threading;
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
        private ImageController? imageController;
        private IEventDTO? eventDTO;
        private GenericContextMenu? contextMenu;
        private CancellationToken ct;

        private void Awake()
        {
            scrollRect.SetScrollSensitivityBasedOnPlatform();

            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventDTO!));
            interestedButton.Button.onClick.AddListener(() => interestedButton.SetSelected(!interestedButton.Selected));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO!));
            permanentJumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO!));
            shareButton.onClick.AddListener(() => OpenContextMenu(shareButton.transform.position));

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ShareText, contextMenuSettings.ShareSprite, () => EventShareButtonClicked?.Invoke(eventDTO!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CopyLinkText, contextMenuSettings.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(eventDTO!)));
        }

        private void OpenContextMenu(Vector2 position) =>
            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, position), ct);

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync(ct);
            closeTasks[1] = closeButton.OnClickAsync(ct);
            return closeTasks;
        }

        public void Configure(IWebRequestController webRequestController) =>
            imageController ??= new ImageController(eventImage, webRequestController);

        public void ConfigureEventData(IEventDTO eventData, PlacesData.PlaceInfo placeData, CancellationToken cancellationToken)
        {
            eventDTO = eventData;
            ct = cancellationToken;

            ResetScrollPosition();

            imageController!.RequestImage(eventData.Image);
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
            for (var i = 0; i < eventData.RecurrentDatesProcessed.Length; i++)
            {
                if (eventData.RecurrentDatesProcessed[i] == default(DateTime)) continue;

                DateTime date = eventData.RecurrentDatesProcessed[i];

                if (date < eventData.NextStartAtProcessed) continue;

                EventUtilities.FormatEventString(date, eventData.Duration, eventSchedulesStringBuilder);

                if (i < eventData.Recurrent_dates.Length - 1)
                    eventSchedulesStringBuilder.Append("\n");
            }

            var result = eventSchedulesStringBuilder.ToString();
            eventSchedulesStringBuilder.Clear();
            return result;
        }

        public void UpdateInterestedButtonState() =>
            interestedButton.SetSelected(eventDTO!.Attending);
    }
}
