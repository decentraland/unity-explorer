using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     What the lobby cards built around a thumbnail share: a click over the whole card, a loading look while the thumbnail is
    ///     on its way, and the thumbnail itself. The hierarchy of a card comes from its UXML template, so its children only exist
    ///     once that template is instantiated: values set before then are kept and applied when the card first attaches to a panel.
    /// </summary>
    public abstract class LobbyThumbnailCardElement : VisualElement
    {
        private const string LOADING_MODIFIER = "--loading";
        private const string THUMBNAIL_NAME = "Thumbnail";

        /// <summary>
        ///     Raised on a click anywhere on the card except its buttons.
        /// </summary>
        public Action? Clicked;

        private readonly string ussLoading;

        private VisualElement? thumbnail;
        private Sprite? thumbnailSprite;

        /// <summary>
        ///     Null falls back to the default thumbnail of the card's stylesheet.
        /// </summary>
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

        /// <summary>
        ///     While false a click on the card raises nothing.
        /// </summary>
        protected virtual bool canClick => true;

        protected LobbyThumbnailCardElement(string ussBlock)
        {
            ussLoading = ussBlock + LOADING_MODIFIER;
            AddToClassList(ussBlock);
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        /// <summary>
        ///     Queries the template children and applies to them what was set before they existed. Runs once, on the first attach.
        /// </summary>
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
            target.style.backgroundImage = LobbyCardBackground.From(thumbnailSprite);
        }

        private void OnClicked()
        {
            if (canClick)
                Clicked?.Invoke();
        }
    }
}
