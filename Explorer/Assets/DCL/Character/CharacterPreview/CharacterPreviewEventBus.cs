using System;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Keeps the shown previews in show order so that only the last one has its container on: preview containers share a layer and a priority,
    ///     so with two of them on every preview brain follows the same virtual camera.
    /// </summary>
    public class CharacterPreviewEventBus
    {
        private readonly List<CharacterPreviewControllerBase> shown = new ();

        public event Action<CharacterPreviewControllerBase>? OnAnyCharacterPreviewShowEvent;
        public event Action<CharacterPreviewControllerBase>? OnAnyCharacterPreviewHideEvent;

        /// <summary>
        ///     The preview shown last among those still shown; the only one whose container should be active.
        /// </summary>
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

        /// <summary>
        ///     Drops a disposed preview without waking the one below it.
        /// </summary>
        public void Forget(CharacterPreviewControllerBase characterPreviewController) =>
            shown.Remove(characterPreviewController);
    }
}
