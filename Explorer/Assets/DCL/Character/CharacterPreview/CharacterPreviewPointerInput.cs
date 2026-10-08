using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Pointer input of a preview in Unity screen pixels (bottom-left origin), whichever UI raised it.
    /// </summary>
    public readonly struct CharacterPreviewPointerInput
    {
        public readonly PointerEventData.InputButton Button;
        public readonly Vector2 Position;
        public readonly Vector2 Delta;
        public readonly Vector2 ScrollDelta;

        public CharacterPreviewPointerInput(PointerEventData.InputButton button, Vector2 position, Vector2 delta, Vector2 scrollDelta = default)
        {
            Button = button;
            Position = position;
            Delta = delta;
            ScrollDelta = scrollDelta;
        }

        public static CharacterPreviewPointerInput From(PointerEventData eventData) =>
            new (eventData.button, eventData.position, eventData.delta, eventData.scrollDelta);
    }
}
