using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class PortableExperiencesSummaryView : ViewBase, IView
    {
        [field: SerializeField]
        internal Button closeButton = null!;
    }
}
