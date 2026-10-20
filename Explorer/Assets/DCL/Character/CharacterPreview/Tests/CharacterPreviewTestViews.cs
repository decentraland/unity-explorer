using Arch.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.CharacterPreview.Tests
{
    public static class CharacterPreviewTestViews
    {
        /// <summary>
        ///     Every object to destroy in teardown is added to <paramref name="created" />.
        /// </summary>
        public static CharacterPreviewView Create(ICollection<Object> created, Transform? parent = null)
        {
            var previewGo = new GameObject("CharacterPreviewView");
            previewGo.transform.SetParent(parent);
            created.Add(previewGo);
            CharacterPreviewView previewView = previewGo.AddComponent<CharacterPreviewView>();

            var settings = ScriptableObject.CreateInstance<CharacterPreviewSettingsSO>();
            created.Add(settings);
            SetBackingField(settings, nameof(CharacterPreviewSettingsSO.cursorSettings), Array.Empty<CharacterPreviewInputCursorSetting>());

            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewInputDetector), previewGo.AddComponent<CharacterPreviewInputDetector>());

            var cursorContainer = previewGo.AddComponent<CharacterPreviewCursorContainer>();
            var cursorGo = new GameObject("CursorOverride", typeof(RectTransform));
            cursorGo.transform.SetParent(previewGo.transform);
            SetBackingField(cursorContainer, nameof(CharacterPreviewCursorContainer.CursorOverrideImage), cursorGo.AddComponent<Image>());
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewCursorContainer), cursorContainer);
            SetBackingField(previewView, nameof(CharacterPreviewView.CharacterPreviewSettingsSo), settings);

            var rawImageGo = new GameObject("RawImage", typeof(RectTransform));
            rawImageGo.transform.SetParent(previewGo.transform);
            SetBackingField(previewView, nameof(CharacterPreviewView.RawImage), rawImageGo.AddComponent<RawImage>());

            var spinnerGo = new GameObject("Spinner");
            spinnerGo.transform.SetParent(previewGo.transform);
            SetBackingField(previewView, nameof(CharacterPreviewView.Spinner), spinnerGo);

            return previewView;
        }

        private static void SetBackingField(object target, string propertyName, object value)
        {
            // Private fields are only reachable through the type that declares them, so base classes are walked explicitly.
            string fieldName = $"<{propertyName}>k__BackingField";

            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        public class TestPreview : CharacterPreviewControllerBase
        {
            public TestPreview(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world, CharacterPreviewEventBus characterPreviewEventBus)
                : base(view, previewFactory, world, false, characterPreviewEventBus) { }
        }
    }
}
