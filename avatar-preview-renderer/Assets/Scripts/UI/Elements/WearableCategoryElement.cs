using System;
using Data;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class WearableCategoryElement : VisualElement
    {
        private const string USS_BLOCK = "wearable-category";
        private const string USS_SELECTED = USS_BLOCK + "--selected";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_ICON_CATEGORY = USS_BLOCK + "__icon--{0}";
        private const string USS_THUMBNAIL = USS_BLOCK + "__thumbnail";

        private readonly WearableItemElement _thumbnail;

        public readonly string Category;
        public readonly string Title;

        public event Action<WearableCategoryElement> Clicked;

        public WearableCategoryElement() : this("upper_body", "UPPER BODY", null)
        {
        }

        public WearableCategoryElement(string category, string title, Texture2D emptyIcon)
        {
            Category = category;
            Title = title;

            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");

            var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
            Add(icon);
            icon.AddToClassList(USS_ICON);
            icon.AddToClassList(string.Format(USS_ICON_CATEGORY, category));

            var label = new Label("<font-weight=600>" + title) { name = "title", pickingMode = PickingMode.Ignore };
            Add(label);
            label.AddToClassList(USS_LABEL);

            Add(_thumbnail = new WearableItemElement { name = "thumbnail", pickingMode = PickingMode.Ignore });
            _thumbnail.EmptyTexture = emptyIcon;
            _thumbnail.AddToClassList(USS_THUMBNAIL);
            _thumbnail.RemoveClickable();
            this.AddManipulator(new AudioClickable(() => Clicked?.Invoke(this)));
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList(USS_SELECTED, selected);
        }

        public void SetWearable(EntityDefinition entity)
        {
            _thumbnail.SetWearable(entity);
        }
    }
}