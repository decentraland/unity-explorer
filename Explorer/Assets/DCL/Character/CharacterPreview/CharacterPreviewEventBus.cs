using System;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewEventBus
    {
        public event Action<CharacterPreviewControllerBase> OnAnyCharacterPreviewShowEvent;
        public event Action<CharacterPreviewControllerBase> OnAnyCharacterPreviewHideEvent;

        public void OnAnyCharacterPreviewShow(CharacterPreviewControllerBase characterPreviewController) =>
            OnAnyCharacterPreviewShowEvent?.Invoke(characterPreviewController);

        public void OnAnyCharacterPreviewHide(CharacterPreviewControllerBase characterPreviewController) =>
            OnAnyCharacterPreviewHideEvent?.Invoke(characterPreviewController);
    }
}
