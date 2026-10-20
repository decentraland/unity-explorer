using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Base of the lobby cards built around a thumbnail. The children come from the UXML template, so values set
    ///     before it is instantiated are applied on the first attach.
    /// </summary>
    public abstract class LobbyThumbnailCardElement : VisualElement
    {
        private const string LOADING_MODIFIER = "--loading";
        private const string THUMBNAIL_NAME = "Thumbnail";

        public Action? Clicked;

        private readonly string ussLoading;

        private VisualElement? thumbnail;
        private Sprite? thumbnailSprite;

        public Sprite? Thumbnail
        {
            get => thumbnailSprite;

            set
            {
                thumbnailSprite = value;

                if (thumbnail != null)
                    ApplyThumbnail(thumbnail);
            }
        }

        public bool IsLoading
        {
            get => ClassListContains(ussLoading);
            set => EnableInClassList(ussLoading, value);
        }

        protected virtual bool canClick => true;

        protected LobbyThumbnailCardElement(string ussBlock)
        {
            ussLoading = ussBlock + LOADING_MODIFIER;
            AddToClassList(ussBlock);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        // Runs once, on the first attach, to query the template children and apply what was set before they existed
        protected abstract void ResolveChildren();

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            if (thumbnail != null)
                return;

            thumbnail = this.Q<VisualElement>(THUMBNAIL_NAME);
            ApplyThumbnail(thumbnail);
            ResolveChildren();
        }

        private void ApplyThumbnail(VisualElement target)
        {
            target.style.backgroundImage = VisualElementsExtensions.CoverBackground(thumbnailSprite);
        }

        private void OnClicked()
        {
            if (canClick)
                Clicked?.Invoke();
        }
    }
}
