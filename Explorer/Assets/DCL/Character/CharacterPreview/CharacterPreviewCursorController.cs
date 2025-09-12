using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using Utility.Ownership;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCursorController : IDisposable
    {
        private readonly CharacterPreviewCursorContainer cursorContainer;
        private readonly CharacterPreviewInputEventBus inputEventBus;
        private readonly Dictionary<CharacterPreviewInputAction, Sprite> cursorReplacementSprites;
        private readonly Box<bool> disposed;

        public CharacterPreviewCursorController(CharacterPreviewCursorContainer cursorContainer, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewInputCursorSetting[] cursorSettings)
        {
            this.cursorContainer = cursorContainer;
            this.inputEventBus = inputEventBus;
            cursorReplacementSprites = DictionaryPool<CharacterPreviewInputAction, Sprite>.Get();
            disposed = new Box<bool>(false);

            for (var index = 0; index < cursorSettings.Length; index++)
            {
                CharacterPreviewInputCursorSetting cursorSetting = cursorSettings[index];
                cursorReplacementSprites.Add(cursorSetting.inputAction, cursorSetting.cursorSprite);
            }

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
            cursorContainer.CursorOverrideImage.transform.position = position;
        }

        private void RestoreCursor()
        {
            cursorContainer.CursorOverrideImage.gameObject.SetActive(false);
            Cursor.visible = true;
        }

        private void ReplaceCursor(PointerEventData pointerEventData)
        {
            static CharacterPreviewInputAction? FromInputButton(PointerEventData.InputButton inputButton) =>
                inputButton switch
                {
                    PointerEventData.InputButton.Right => CharacterPreviewInputAction.VerticalPan,
                    PointerEventData.InputButton.Left => CharacterPreviewInputAction.Rotate,
                    _ => null,
                };

            CharacterPreviewInputAction? action = FromInputButton(pointerEventData.button);
            if (action.HasValue && cursorReplacementSprites.TryGetValue(action.Value, out Sprite sprite))
            {
                cursorContainer.CursorOverrideImage.sprite = sprite!;
                MoveCursor(pointerEventData.position);
                cursorContainer.CursorOverrideImage.gameObject.SetActive(true);
                Cursor.visible = false;
                cursorContainer.CursorOverrideImage.SetNativeSize();
            }
        }

        public void Dispose()
        {
            if (disposed.Value)
            {
                ReportHub.LogError(ReportCategory.UI, "Attempt to double dispose");
                return;
            }

            disposed.Value = true;

            inputEventBus.OnPointerUpEvent -= OnPointerUp;
            inputEventBus.OnPointerDownEvent -= OnPointerDown;
            inputEventBus.OnDraggingEvent -= OnDrag;

            DictionaryPool<CharacterPreviewInputAction, Sprite>.Release(cursorReplacementSprites);
        }
    }
}
