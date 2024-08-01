using DCL.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class ProfileWidgetView : ViewBase, IView
    {
        [field: SerializeField] public ImageView FaceSnapshotImage { get; private set; } = null!;

        [field: SerializeField] public TMP_Text? NameLabel { get; private set; }

        [field: SerializeField] public TMP_Text? AddressLabel { get; private set; }

        [field: SerializeField] public Button OpenProfileButton { get; private set; } = null!;
    }
}
