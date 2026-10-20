using System;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Keeps the shown previews in show order and tells the new top to restore its container when the top one hides.
    ///     Main-thread only; not thread-safe.
    /// </summary>
    public class CharacterPreviewEventBus
    {
        private readonly List<CharacterPreviewControllerBase> shown = new ();

        public event Action<CharacterPreviewControllerBase>? OnAnyCharacterPreviewShowEvent;
        public event Action<CharacterPreviewControllerBase>? OnCharacterPreviewRestoredEvent;

        public event Action<bool>? OnAnyShownChangedEvent;

        public CharacterPreviewControllerBase? Top => shown.Count > 0 ? shown[^1] : null;

        public bool AnyShown => shown.Count > 0;

        public void OnAnyCharacterPreviewShow(CharacterPreviewControllerBase characterPreviewController)
        {
            bool wasAnyShown = AnyShown;
            shown.Remove(characterPreviewController);
            shown.Add(characterPreviewController);
            OnAnyCharacterPreviewShowEvent?.Invoke(characterPreviewController);
            RaiseAnyShownChangedIfCrossed(wasAnyShown);
        }

        public void OnAnyCharacterPreviewHide(CharacterPreviewControllerBase characterPreviewController)
        {
            bool wasAnyShown = AnyShown;
            bool wasTop = Top == characterPreviewController;
            shown.Remove(characterPreviewController);

            if (wasTop && Top != null)
                OnCharacterPreviewRestoredEvent?.Invoke(Top);

            RaiseAnyShownChangedIfCrossed(wasAnyShown);
        }

        // A disposed preview leaves without waking the one below it.
        public void Forget(CharacterPreviewControllerBase characterPreviewController)
        {
            bool wasAnyShown = AnyShown;
            shown.Remove(characterPreviewController);
            RaiseAnyShownChangedIfCrossed(wasAnyShown);
        }

        private void RaiseAnyShownChangedIfCrossed(bool wasAnyShown)
        {
            if (wasAnyShown != AnyShown)
                OnAnyShownChangedEvent?.Invoke(AnyShown);
        }
    }
}
