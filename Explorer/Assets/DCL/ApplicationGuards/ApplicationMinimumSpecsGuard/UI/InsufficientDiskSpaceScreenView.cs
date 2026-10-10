using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationGuards
{
    public class InsufficientDiskSpaceScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Button QuitButton { get; private set; } = null!;
    }
}
