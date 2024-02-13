using DCL.CharacterPreview;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Backpack.CharacterPreview
{
    public readonly struct BackpackCharacterPreviewCursorController : IDisposable
    {
        private readonly BackpackCharacterPreviewCursorView view;
        private readonly CharacterPreviewInputEventBus inputEventBus;
        public BackpackCharacterPreviewCursorController(BackpackCharacterPreviewCursorView view, CharacterPreviewInputEventBus inputEventBus)
        {
            this.view = view;
            this.inputEventBus = inputEventBus;

            inputEventBus.OnPointerUpEvent += OnPointerUp;
            inputEventBus.OnPointerDownEvent += OnPointerDown;
            inputEventBus.OnDraggingEvent += OnDrag;
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            MoveCursor(pointerEventData.position);
        }

        private void OnPointerUp(PointerEventData pointerEventData)
        {
            RestoreCursor();
        }

        private void OnPointerDown(PointerEventData pointerEventData)
        {
            ReplaceCursor(pointerEventData);
        }

        private void MoveCursor(Vector2 position)
        {
            view.CursorOverrideImage.transform.position = position;
        }

        private void RestoreCursor()
        {
            view.CursorOverrideImage.gameObject.SetActive(false);
            Cursor.visible = true;
        }

        private void ReplaceCursor(PointerEventData pointerEventData)
        {
            if (pointerEventData.button != PointerEventData.InputButton.Middle)
            {
                MoveCursor(pointerEventData.position);
                view.CursorOverrideImage.gameObject.SetActive(true);
                Cursor.visible = false;
                view.CursorOverrideImage.SetNativeSize();

                switch (pointerEventData.button)
                {
                    case PointerEventData.InputButton.Right:
                        view.CursorOverrideImage.sprite = view.panCursor;
                        break;
                    case PointerEventData.InputButton.Left:
                        view.CursorOverrideImage.sprite = view.rotateCursor;
                        break;
                }
            }
        }

        public void Dispose()
        {
            inputEventBus.OnPointerUpEvent -= OnPointerUp;
            inputEventBus.OnPointerDownEvent -= OnPointerDown;
            inputEventBus.OnDraggingEvent -= OnDrag;
        }
    }
}
