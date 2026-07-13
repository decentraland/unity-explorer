using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationsGuards
{
    public class LivekitHealthGuardView : ViewBase, IView
    {

        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;


    }
}
