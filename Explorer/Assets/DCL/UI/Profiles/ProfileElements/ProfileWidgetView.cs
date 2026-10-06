using DCL.UI.Buttons;
using DCL.Utilities;
using MVC;
using TMPro;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public class ProfileWidgetView : ViewBase, IView, IProfileWidgetView
    {
        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; } = null!;
        [field: SerializeField] public HoverableButton OpenProfileButton { get; private set; } = null!;

        [Header("Can be Null")]
        [field: SerializeField] public TMP_Text? NameLabel { get; private set; }

        [field: SerializeField] public TMP_Text? AddressLabel { get; private set; }

        public string Name
        {
            set
            {
                if (NameLabel != null)
                    NameLabel.text = value;
            }
        }

        public string Address
        {
            set
            {
                if (AddressLabel != null)
                    AddressLabel.text = value;
            }
        }

        public void BindThumbnail(IReactiveProperty<ProfileThumbnailViewModel> thumbnail) =>
            ProfilePictureView.Bind(thumbnail);
    }
}
