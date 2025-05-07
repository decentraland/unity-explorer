using DCL.UI.ProfileElements;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI
{
    public class UnfriendConfirmationPopupView : PopupViewBase, IView, IViewWithGlobalDependencies
    {
        [field: SerializeField]
        public Button CancelButton { get; private set; }

        [field: SerializeField]
        public Button ConfirmButton { get; private set; }

        [field: SerializeField]
        public TMP_Text DescriptionLabel { get; private set; }

        [field: SerializeField]
        public ProfilePictureView ProfilePicture { get; private set; }

        public void InjectDependencies(ViewDependencies dependencies) =>
            ProfilePicture.InjectDependencies(dependencies);
    }
}
