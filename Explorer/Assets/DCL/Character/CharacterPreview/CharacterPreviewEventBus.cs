using System;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Show-order stack of the previews on screen; only the top one has its container on.
    /// </summary>
    public class CharacterPreviewEventBus
    {
        private readonly List<CharacterPreviewControllerBase> shown = new ();

        public event Action<CharacterPreviewControllerBase>? OnAnyCharacterPreviewShowEvent;
        public event Action<CharacterPreviewControllerBase>? OnAnyCharacterPreviewHideEvent;

        public CharacterPreviewControllerBase? Top => shown.Count > 0 ? shown[^1] : null;

        public void OnAnyCharacterPreviewShow(CharacterPreviewControllerBase characterPreviewController)
        {
            shown.Remove(characterPreviewController);
            shown.Add(characterPreviewController);
            OnAnyCharacterPreviewShowEvent?.Invoke(characterPreviewController);
        }

        public void OnAnyCharacterPreviewHide(CharacterPreviewControllerBase characterPreviewController)
        {
            shown.Remove(characterPreviewController);
            OnAnyCharacterPreviewHideEvent?.Invoke(characterPreviewController);
        }

        // A disposed preview leaves without waking the one below it.
        public void Forget(CharacterPreviewControllerBase characterPreviewController) =>
            shown.Remove(characterPreviewController);
    }
}
