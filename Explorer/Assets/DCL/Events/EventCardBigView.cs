using UnityEngine;

namespace DCL.Events
{
    public class EventCardBigView : EventCardView
    {
        [Header("Animations")]
        [SerializeField] private CanvasGroup hostCanvasGroup = null!;

        protected override void PlayHoverAnimation()
        {

        }

        protected override void PlayHoverExitAnimation(bool instant = false)
        {

        }
    }
}
