using UnityEngine;

namespace DCL.Events
{
    public class EventsView : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private EventsCalendarView eventsCalendarView = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        public EventsCalendarView EventsCalendarView => eventsCalendarView;

        public void SetViewActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }
    }
}
