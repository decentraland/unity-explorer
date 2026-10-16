using System;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputEventBus
    {
        public event Action<CharacterPreviewPointerInput>? OnDraggingEvent;
        public event Action<CharacterPreviewPointerInput>? OnScrollEvent;
        public event Action<CharacterPreviewPointerInput>? OnPointerUpEvent;
        public event Action<CharacterPreviewPointerInput>? OnPointerDownEvent;
        public event Action<AvatarWearableCategoryEnum>? OnChangePreviewFocusEvent;

        public void OnDrag(in CharacterPreviewPointerInput input) =>
            OnDraggingEvent?.Invoke(input);

        public void OnScroll(in CharacterPreviewPointerInput input) =>
            OnScrollEvent?.Invoke(input);

        public void OnChangePreviewFocus(AvatarWearableCategoryEnum category) =>
            OnChangePreviewFocusEvent?.Invoke(category);

        public void OnPointerUp(in CharacterPreviewPointerInput input) =>
            OnPointerUpEvent?.Invoke(input);

        public void OnPointerDown(in CharacterPreviewPointerInput input) =>
            OnPointerDownEvent?.Invoke(input);
    }
}
