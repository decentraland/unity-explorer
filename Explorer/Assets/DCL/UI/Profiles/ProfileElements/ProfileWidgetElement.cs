using DCL.Utilities;
using System;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.UI.ProfileElements
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="ProfileWidgetView" />: the picture of the current user next to the name, with the wallet
    ///     tag under it while there is one; a click anywhere on it is reported through <see cref="Clicked" />. It builds its own children,
    ///     so it is complete wherever it is created; ProfileWidget.uss styles them, so the document that hosts it imports that stylesheet.
    /// </summary>
    [UxmlElement]
    public partial class ProfileWidgetElement : VisualElement, IProfileWidgetView
    {
        private const string USS_BLOCK = "profile-widget";
        private const string USS_LOADING = USS_BLOCK + "--loading";
        private const string USS_PICTURE = USS_BLOCK + "__picture";
        private const string USS_TEXTS = USS_BLOCK + "__texts";
        private const string USS_NAME = USS_BLOCK + "__name";
        private const string USS_ADDRESS = USS_BLOCK + "__address";

        private const string PICTURE_NAME = "Picture";
        private const string TEXTS_NAME = "Texts";
        private const string NAME_NAME = "Name";
        private const string ADDRESS_NAME = "Address";

        private readonly VisualElement picture;
        private readonly Label nameLabel;
        private readonly Label addressLabel;

        private IDisposable? thumbnailSubscription;

        public Action? Clicked;

        [UxmlAttribute]
        public string Name
        {
            get => nameLabel.text;
            set => nameLabel.text = value;
        }

        /// <summary>
        ///     Hidden while empty.
        /// </summary>
        [UxmlAttribute]
        public string Address
        {
            get => addressLabel.text;

            set
            {
                addressLabel.text = value;
                addressLabel.SetDisplayed(!string.IsNullOrEmpty(value));
            }
        }

        /// <summary>
        ///     True while a picture is being fetched and there is no previous one to show meanwhile.
        /// </summary>
        public bool IsLoading => ClassListContains(USS_LOADING);

        public ProfileWidgetElement()
        {
            AddToClassList(USS_BLOCK);

            picture = new VisualElement { name = PICTURE_NAME, pickingMode = PickingMode.Ignore };
            picture.AddToClassList(USS_PICTURE);
            Add(picture);

            var texts = new VisualElement { name = TEXTS_NAME, pickingMode = PickingMode.Ignore };
            texts.AddToClassList(USS_TEXTS);
            Add(texts);

            nameLabel = new Label { name = NAME_NAME, pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList(USS_NAME);
            texts.Add(nameLabel);

            addressLabel = new Label { name = ADDRESS_NAME, pickingMode = PickingMode.Ignore };
            addressLabel.AddToClassList(USS_ADDRESS);
            texts.Add(addressLabel);

            Address = string.Empty;
            this.AddManipulator(new Clickable(OnClicked));
        }

        /// <summary>
        ///     Draws the picture <paramref name="thumbnail" /> resolves to from now on, letting go of the one it followed before.
        /// </summary>
        public void BindThumbnail(IReactiveProperty<ProfileThumbnailViewModel> thumbnail)
        {
            thumbnailSubscription?.Dispose();

            thumbnail.TryBind();
            OnThumbnailUpdated(thumbnail.Value);
            thumbnailSubscription = thumbnail.Subscribe(OnThumbnailUpdated);
        }

        // The name takes the profile color; without a picture it fills the circle too, and a fetch keeps the previous picture up while there is one
        private void OnThumbnailUpdated(ProfileThumbnailViewModel model)
        {
            nameLabel.style.color = model.ProfileColor;
            picture.style.backgroundColor = model.ProfileColor;
            picture.style.backgroundImage = model.Sprite == null ? StyleKeyword.Null : new StyleBackground(model.Sprite);
            EnableInClassList(USS_LOADING, model.ThumbnailState == ProfileThumbnailViewModel.State.Loading && model.Sprite == null);
        }

        private void OnClicked() =>
            Clicked?.Invoke();
    }
}
