using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Events;
using DCL.EventsApi;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.WebRequests;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.EventInfo
{
    public class EventInfoView: ViewBase, IView
    {
        [SerializeField] private Button backgroundCloseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private CommunityEventsContextMenuConfiguration contextMenuConfiguration;

        [Header("Event Info")]
        [SerializeField] private ImageView eventImage;
        [SerializeField] private TMP_Text eventDate;
        [SerializeField] private TMP_Text eventName;
        [SerializeField] private TMP_Text hostName;
        [SerializeField] private TMP_Text interestedCounter;
        [SerializeField] private ButtonWithSelectableStateView interestedButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private TMP_Text eventDescription;
        [SerializeField] private TMP_Text eventSchedules;

        public event Action<IEventDTO>? InterestedButtonClicked;
        public event Action<IEventDTO>? EventShareButtonClicked;
        public event Action<IEventDTO>? EventCopyLinkButtonClicked;

        private readonly UniTask[] closeTasks = new UniTask[2];
        private ImageController imageController;
        private IMVCManager mvcManager;
        private IEventDTO eventDTO;
        private GenericContextMenu contextMenu;

        private void Awake()
        {
            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventDTO));
            interestedButton.Button.onClick.AddListener(() => InterestedButtonClicked?.Invoke(eventDTO));
            shareButton.onClick.AddListener(() => OpenContextMenu(shareButton.transform.position));

            contextMenu = new GenericContextMenu(contextMenuConfiguration.ContextMenuWidth, verticalLayoutPadding: contextMenuConfiguration.VerticalPadding, elementsSpacing: contextMenuConfiguration.ElementsSpacing)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.ShareText, contextMenuConfiguration.ShareSprite, () => EventShareButtonClicked?.Invoke(eventDTO)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuConfiguration.CopyLinkText, contextMenuConfiguration.CopyLinkSprite, () => EventCopyLinkButtonClicked?.Invoke(eventDTO)));
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

        public void ConfigureEventData(IEventDTO eventData)
        {
            eventDTO = eventData;

            imageController.RequestImage(eventData.Image);
        }
    }
}
