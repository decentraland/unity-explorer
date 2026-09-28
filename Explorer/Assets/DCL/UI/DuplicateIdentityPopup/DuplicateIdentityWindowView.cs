using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowView : ViewBase, IView
    {
        [field: SerializeField] public Button ExitButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text Title { get; private set; } = null!;
        [field: SerializeField] public TMP_Text Description { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ActionLabel { get; private set; } = null!;
    }
}


