using System;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewEventBus
    {
        public event Action<CharacterPreviewType> OnAnyCharacterPreviewShowEvent;
        public event Action<CharacterPreviewType> OnAnyCharacterPreviewHideEvent;

        public void OnAnyCharacterPreviewShow(CharacterPreviewType type) =>
            OnAnyCharacterPreviewShowEvent?.Invoke(type);

        public void OnAnyCharacterPreviewHide(CharacterPreviewType type) =>
            OnAnyCharacterPreviewHideEvent?.Invoke(type);
    }
}
