using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.EventInfo
{
    public class EventInfoView: ViewBase, IView
    {
        [SerializeField] private Button backgroundCloseButton;
        [SerializeField] private Button closeButton;

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

        private readonly UniTask[] closeTasks = new UniTask[2];
        private ImageController imageController;

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync();
            closeTasks[1] = closeButton.OnClickAsync();
            return closeTasks;
        }

        public void ConfigureEventData(IEventDTO eventData, IWebRequestController webRequestController)
        {
            imageController ??= new ImageController(eventImage, webRequestController);

            imageController.RequestImage(eventData.Image);
        }
    }
}
