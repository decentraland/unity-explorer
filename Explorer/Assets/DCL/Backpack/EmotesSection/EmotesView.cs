using DCL.Backpack.EmotesSection;
using UnityEngine;

namespace DCL.Backpack
{
    public class EmotesView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackInfoPanelView BackpackInfoPanelView { get; private set; } = null!;

        [field: SerializeField]
        public BackpackGridView GridView { get; private set; } = null!;

        [field: SerializeField]
        public BackpackEmoteBreadCrumbView BreadCrumb { get; private set; } = null!;

        [field: SerializeField]
        public EmoteSlotContainerView[] Slots { get; set; } = null!;
    }
}
