using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCursorController : IDisposable
    {
        private readonly CharacterPreviewCursorContainer cursorContainer;
        private readonly CharacterPreviewInputEventBus inputEventBus;
        private readonly Dictionary<CharacterPreviewInputAction, Sprite> cursorReplacementSprites;

        public CharacterPreviewCursorController(CharacterPreviewCursorContainer cursorContainer, CharacterPreviewInputEventBus inputEventBus, CharacterPreviewInputCursorSetting[] cursorSettings)
        {
            this.cursorContainer = cursorContainer;
            this.inputEventBus = inputEventBus;
            cursorReplacementSprites = DictionaryPool<CharacterPreviewInputAction, Sprite>.Get();

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
            if (pointerEventData.button != PointerEventData.InputButton.Middle)
            {
                var cursorOverridePresent = false;

                switch (pointerEventData.button)
                {
                    case PointerEventData.InputButton.Right:
                        cursorOverridePresent = cursorReplacementSprites.TryGetValue(CharacterPreviewInputAction.VerticalPan, out Sprite panSprite);

                        if (cursorOverridePresent) { cursorContainer.CursorOverrideImage.sprite = panSprite; }

                        break;
                    case PointerEventData.InputButton.Left:
                        cursorOverridePresent = cursorReplacementSprites.TryGetValue(CharacterPreviewInputAction.Rotate, out Sprite rotateSprite);

                        if (cursorOverridePresent) { cursorContainer.CursorOverrideImage.sprite = rotateSprite; }

                        break;
                }

                if (cursorOverridePresent)
                {
                    MoveCursor(pointerEventData.position);
                    cursorContainer.CursorOverrideImage.gameObject.SetActive(true);
                    Cursor.visible = false;
                    cursorContainer.CursorOverrideImage.SetNativeSize();
                }
            }
        }

        public void Dispose()
        {
            inputEventBus.OnPointerUpEvent -= OnPointerUp;
            inputEventBus.OnPointerDownEvent -= OnPointerDown;
            inputEventBus.OnDraggingEvent -= OnDrag;

            DictionaryPool<CharacterPreviewInputAction, Sprite>.Release(cursorReplacementSprites);
        }
    }
}
