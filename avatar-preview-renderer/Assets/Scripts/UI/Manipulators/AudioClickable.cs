using System;
using Services;
using UnityEngine.UIElements;

namespace UI.Manipulators
{
    public class AudioClickable : Clickable
    {
        public AudioClickable(Action handler) : base(handler)
        {
            clicked += InternalClicked;
        }

        private void InternalClicked()
        {
            AudioService.Instance?.PlaySFX(SFXType.UIClick);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            base.RegisterCallbacksOnTarget();
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            base.UnregisterCallbacksFromTarget();
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            AudioService.Instance?.PlaySFX(SFXType.UIHover);
        }
    }
}