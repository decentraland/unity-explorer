using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.Utilities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelView: ViewBase, IView
    {
        private const string HOST_FORMAT = "Hosted by <b>{0}</b>";
        private const int SCHEDULES_POOL_DEFAULT_CAPACITY = 10;

        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;
        [SerializeField] private EventContextMenuConfiguration contextMenuSettings = null!;
        [SerializeField] private ScrollRect scrollRect = null!;

        [Header("Event Info")]
        [SerializeField] private ImageView eventImage = null!;
        [SerializeField] private TMP_Text eventDate = null!;
        [SerializeField] private TMP_Text eventName = null!;
        [SerializeField] private TMP_Text hostName = null!;
        [SerializeField] private ButtonWithSelectableStateView interestedButton = null!;
        [SerializeField] private Button addToCalendarButton = null!;
        [SerializeField] private Button shareButton = null!;
        [SerializeField] private Button jumpInButton = null!;
        [SerializeField] private Button permanentJumpInButton = null!;
        [SerializeField] private TMP_Text eventDescription = null!;
        [SerializeField] private Transform schedulesContainer = null!;
        [SerializeField] private EventScheduleRow scheduleRowPrefab = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private GameObject liveBadge = null!;

        public event Action<IEventDTO>? InterestedButtonClicked;
        public event Action<IEventDTO>? JumpInButtonClicked;
        public event Action<IEventDTO>? AddToCalendarButtonClicked;
        public event Action<IEventDTO, DateTime>? AddRecurrentDateToCalendarButtonClicked;
        public event Action<IEventDTO>? EventShareButtonClicked;
        public event Action<IEventDTO>? EventCopyLinkButtonClicked;

        private readonly UniTask[] closeTasks = new UniTask[2];
        private IEventDTO? eventDTO;
        private GenericContextMenu? contextMenu;
        private CancellationToken ct;
        private IObjectPool<EventScheduleRow> schedulesPool = null!;
        private readonly List<EventScheduleRow> currentScheduleRows = new ();

        private void Awake()
        {
            scrollRect.SetScrollSensitivityBasedOnPlatform();

            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventDTO!));
            jumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO!));
            permanentJumpInButton.onClick.AddListener(() => JumpInButtonClicked?.Invoke(eventDTO!));
            addToCalendarButton.onClick.AddListener(() => AddToCalendarButtonClicked?.Invoke(eventDTO!));
            shareButton.onClick.AddListener(() => OpenContextMenu(shareButton.transform.position));

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ShareText, contextMenuSettings.ShareSprite, () => EventShareButtonClicked?.Invoke(eventDTO!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CopyLinkText, contextMenuSettings.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(eventDTO!)));

            // Schedules pool configuration
            schedulesPool = new ObjectPool<EventScheduleRow>(
                InstantiateScheduleRow,
                defaultCapacity: SCHEDULES_POOL_DEFAULT_CAPACITY,
                actionOnGet: scheduleRow =>
                {
                    scheduleRow.gameObject.SetActive(true);
                    scheduleRow.transform.SetAsLastSibling();
                },
                actionOnRelease: dotButtonView => dotButtonView.gameObject.SetActive(false));
        }

        private EventScheduleRow InstantiateScheduleRow()
        {
            EventScheduleRow scheduleRow = Instantiate(scheduleRowPrefab, schedulesContainer);
            return scheduleRow;
        }

        private void OpenContextMenu(Vector2 position) =>
            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, position), ct);

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync(ct);
            closeTasks[1] = closeButton.OnClickAsync(ct);
            return closeTasks;
        }

        public void ConfigureEventData(IEventDTO eventData, PlacesData.PlaceInfo? placeData, ThumbnailLoader thumbnailLoader, CancellationToken cancellationToken)
        {
            eventDTO = eventData;
            ct = cancellationToken;

            ResetScrollPosition();

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(eventData.Image, eventImage, null, cancellationToken, true).Forget();
            eventDate.text = EventUtilities.GetEventTimeText(eventData);
            eventName.text = eventData.Name;
            hostName.text = string.Format(HOST_FORMAT, eventData.User_name);
            UpdateInterestedButtonState(eventData.Attending);
            eventDescription.text = eventData.Description;
            jumpInButton.gameObject.SetActive(eventData.Live);
            interestedButton.gameObject.SetActive(!eventData.Live);
            liveBadge.SetActive(eventData.Live);

            if (placeData != null)
                placeNameText.text = eventData.World ? $"{placeData.title} ({placeData.world_name})" : $"{placeData.title} ({eventData.X},{eventData.Y})";
            else
                placeNameText.text = $"{eventData.Scene_name} ({eventData.X},{eventData.Y})";

            GenerateRecurrentSchedules(eventData);
        }

        private void ResetScrollPosition() =>
            scrollRect.verticalNormalizedPosition = 1f;

        private void GenerateRecurrentSchedules(IEventDTO eventData)
        {
            ClearSchedules();

            foreach (var recurrentDate in eventData.RecurrentDatesProcessed)
            {
                if (recurrentDate == default(DateTime)) continue;

                DateTime date = recurrentDate;

                if (date < eventData.NextStartAtProcessed) continue;

                var scheduleRow = schedulesPool.Get();
                scheduleRow.Configure(eventData, date);
                scheduleRow.AddToCalendarClicked -= OnAddRecurrentDateToCalendarClicked;
                scheduleRow.AddToCalendarClicked += OnAddRecurrentDateToCalendarClicked;
                currentScheduleRows.Add(scheduleRow);
            }
        }

        private void ClearSchedules()
        {
            foreach (EventScheduleRow scheduleRow in currentScheduleRows)
                schedulesPool.Release(scheduleRow);

            currentScheduleRows.Clear();
        }

        private void OnAddRecurrentDateToCalendarClicked(IEventDTO eventInfo, DateTime utcStart) =>
            AddRecurrentDateToCalendarButtonClicked?.Invoke(eventInfo, utcStart);

        public void UpdateInterestedButtonState(bool isInterested)
        {
            eventDTO!.Attending = isInterested;
            interestedButton.SetSelected(isInterested);
        }
    }
}
