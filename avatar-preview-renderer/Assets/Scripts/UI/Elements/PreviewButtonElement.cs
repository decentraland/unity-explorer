using System;
using UI.Manipulators;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class PreviewButtonElement: VisualElement
    {
        private const string USS_BLOCK = "preview-button";
        private const string USS_CONTAINER = USS_BLOCK + "__container";
        private const string USS_THUMBNAIL = USS_BLOCK + "__thumbnail";
        private const string USS_SELECTED = USS_BLOCK + "--selected";

        public bool Selected
        {
            get => ClassListContains(USS_SELECTED);
            set => EnableInClassList(USS_SELECTED, value);
        }
        
        public event Action Clicked;
        
        private readonly VisualElement _thumbnail;

        private AudioClickable _clickable;

        public PreviewButtonElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");
            
            var container = new VisualElement { name = "container", pickingMode = PickingMode.Ignore };
            Add(container);
            container.AddToClassList(USS_CONTAINER);
            {
                container.Add(_thumbnail = new VisualElement { name = "thumbnail", pickingMode = PickingMode.Ignore });
                _thumbnail.AddToClassList(USS_THUMBNAIL);
            }
            
            this.AddManipulator(_clickable = new AudioClickable(() => Clicked?.Invoke()));
        }

        public void RemoveClickable()
        {
            this.RemoveManipulator(_clickable);
            RemoveFromClassList("dcl-clickable");
            _clickable = null;
        }

        public void SetTexture(Texture2D texture)
        {
            _thumbnail.style.backgroundImage = texture;
        }
    }
}