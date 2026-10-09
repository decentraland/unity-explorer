using DCL.Utilities;
using System;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.UI.ProfileElements
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="ProfileWidgetView" />. It builds its own children; the hosting document
    ///     imports ProfileWidget.uss.
    /// </summary>
    [UxmlElement]
    public partial class ProfileWidgetElement : VisualElement, IProfileWidgetView, IDisposable
    {
        private const string USS_BLOCK = "profile-widget";
        private const string USS_LOADING = USS_BLOCK + "--loading";
        private const string USS_PICTURE = USS_BLOCK + "__picture";
        private const string USS_TEXTS = USS_BLOCK + "__texts";
        private const string USS_NAME = USS_BLOCK + "__name";
        private const string USS_ADDRESS = USS_BLOCK + "__address";
        private const string USS_ADDRESS_EMPTY = USS_ADDRESS + "--empty";

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

        [UxmlAttribute]
        public string Address
        {
            get => addressLabel.text;

            set
            {
                addressLabel.text = value;
                addressLabel.EnableInClassList(USS_ADDRESS_EMPTY, string.IsNullOrEmpty(value));
            }
        }

        /// <summary>
        ///     True while a picture is being fetched and there is no previous one to show meanwhile.
        /// </summary>
        public bool IsLoading => ClassListContains(USS_LOADING);

        public ProfileWidgetElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);

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

        public void BindThumbnail(IReactiveProperty<ProfileThumbnailViewModel> thumbnail)
        {
            thumbnailSubscription?.Dispose();

            thumbnail.TryBind();
            OnThumbnailUpdated(thumbnail.Value);
            thumbnailSubscription = thumbnail.Subscribe(OnThumbnailUpdated);
        }

        public void Dispose()
        {
            thumbnailSubscription?.Dispose();
            thumbnailSubscription = null;
        }

        private void OnThumbnailUpdated(ProfileThumbnailViewModel model)
        {
            picture.SetProfileColor(model.ProfileColor);
            picture.style.backgroundImage = VisualElementsExtensions.CoverBackground(model.Sprite);
            EnableInClassList(USS_LOADING, model.ThumbnailState == ProfileThumbnailViewModel.State.Loading && model.Sprite == null);
        }

        private void OnClicked() =>
            Clicked?.Invoke();
    }
}
