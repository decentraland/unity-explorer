using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowView : ViewBase, IView
    {
        [field: SerializeField] public Button ExitButton { get; private set; } = null!;
        [field: SerializeField] public Button RestartButton { get; private set; } = null!;
    }
}


