using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Utilities;
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

        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;
        [SerializeField] private EventInfoContextMenuConfiguration contextMenuSettings = null!;
        [SerializeField] private ScrollRect scrollRect = null!;
        [field: SerializeField] public WarningNotificationView SuccessNotificationView { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView ErrorNotificationView { get; private set; } = null!;

        [Header("Event Info")]
        [SerializeField] private ImageView eventImage = null!;
        [SerializeField] private TMP_Text eventDate = null!;
        [SerializeField] private TMP_Text eventName = null!;
        [SerializeField] private TMP_Text hostName = null!;
        [SerializeField] private ButtonWithSelectableStateView interestedButton = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private Button permanentJumpInButton = null!;
        [SerializeField] private TMP_Text eventDescription = null!;
        [SerializeField] private TMP_Text eventSchedules = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private GameObject liveBadge = null!;

        public event Action<IEventDTO>? InterestedButtonClicked;
        public event Action<IEventDTO>? JumpInButtonClicked;
        public event Action<IEventDTO>? EventShareButtonClicked;
        public event Action<IEventDTO>? EventCopyLinkButtonClicked;

        private readonly UniTask[] closeTasks = new UniTask[2];
        private readonly StringBuilder eventSchedulesStringBuilder = new ();
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

        public void ConfigureEventData(IEventDTO eventData, PlacesData.PlaceInfo placeData, ThumbnailLoader thumbnailLoader, CancellationToken cancellationToken)
        {
            eventDTO = eventData;
            ct = cancellationToken;

            ResetScrollPosition();

            thumbnailLoader.LoadCommunityThumbnailAsync(new Uri(eventData.Image), eventImage, null, cancellationToken).Forget();
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
